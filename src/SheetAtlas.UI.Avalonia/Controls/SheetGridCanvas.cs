using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.UI.Avalonia.Controls;

/// <summary>
/// Custom-rendered spreadsheet grid for visualizing sheet data and selecting regions.
/// Uses DrawingContext for performance — no UI element per cell.
/// Designed to sit inside a ScrollViewer; reports full logical size and renders only the visible viewport.
/// </summary>
public class SheetGridCanvas : Control
{
    private const double RowHeaderWidth = 50;
    private const double ColumnHeaderHeight = 24;
    private const double CellHeight = 22;
    private const double MinCellWidth = 20;
    private const double MaxCellWidth = 120;
    private const double DefaultCellWidth = 80;
    private const double CellTextPadding = 4;

    // Cached computed values
    private double[] _columnWidths = Array.Empty<double>();
    private double _totalWidth;
    private double _totalHeight;

    // Drag selection state
    private bool _isDragging;
    private int _dragStartRow;
    private int _dragStartCol;
    private int _dragCurrentRow;
    private int _dragCurrentCol;

    // Resize state (bottom-edge drag on active region)
    private bool _isResizing;
    private int _resizeStartRow;
    private const double ResizeHitZone = 5.0;

    /// <summary>
    /// Fired when the user finishes resizing the active region's bottom edge.
    /// The event arg contains the updated DataRegion with new DataEndRow.
    /// </summary>
    public event EventHandler<DataRegion>? RegionResizeCompleted;

    #region Styled Properties

    public static readonly StyledProperty<SASheetData?> SheetDataProperty =
        AvaloniaProperty.Register<SheetGridCanvas, SASheetData?>(nameof(SheetData));

    public static readonly StyledProperty<IReadOnlyDictionary<string, DataRegion>?> RegionsProperty =
        AvaloniaProperty.Register<SheetGridCanvas, IReadOnlyDictionary<string, DataRegion>?>(nameof(Regions));

    public static readonly StyledProperty<DataRegion?> SelectionRegionProperty =
        AvaloniaProperty.Register<SheetGridCanvas, DataRegion?>(nameof(SelectionRegion),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<DataRegion?> ActiveRegionProperty =
        AvaloniaProperty.Register<SheetGridCanvas, DataRegion?>(nameof(ActiveRegion),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<SheetGridCanvas, bool>(nameof(IsReadOnly));

    public static readonly StyledProperty<bool> IsEditModeProperty =
        AvaloniaProperty.Register<SheetGridCanvas, bool>(nameof(IsEditMode),
            defaultBindingMode: BindingMode.TwoWay);

    public SASheetData? SheetData
    {
        get => GetValue(SheetDataProperty);
        set => SetValue(SheetDataProperty, value);
    }

    public IReadOnlyDictionary<string, DataRegion>? Regions
    {
        get => GetValue(RegionsProperty);
        set => SetValue(RegionsProperty, value);
    }

    /// <summary>
    /// Output: the region currently being selected via drag (two-way for ViewModel binding).
    /// Null when no active selection.
    /// </summary>
    public DataRegion? SelectionRegion
    {
        get => GetValue(SelectionRegionProperty);
        set => SetValue(SelectionRegionProperty, value);
    }

    /// <summary>
    /// The region activated by clicking its badge. Two-way bound to ViewModel.
    /// Null when no region is active.
    /// </summary>
    public DataRegion? ActiveRegion
    {
        get => GetValue(ActiveRegionProperty);
        set => SetValue(ActiveRegionProperty, value);
    }

    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>
    /// When true, enables bottom-edge resize on the active region.
    /// Bound to ViewModel's IsEditingRegion.
    /// </summary>
    public bool IsEditMode
    {
        get => GetValue(IsEditModeProperty);
        set => SetValue(IsEditModeProperty, value);
    }

    #endregion

    static SheetGridCanvas()
    {
        AffectsRender<SheetGridCanvas>(SheetDataProperty, RegionsProperty, SelectionRegionProperty, ActiveRegionProperty);
        AffectsMeasure<SheetGridCanvas>(SheetDataProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var sheet = SheetData;
        if (sheet == null || sheet.RowCount == 0 || sheet.ColumnCount == 0)
            return new Size(200, 50);

        ComputeColumnWidths(availableSize.Width);
        _totalWidth = RowHeaderWidth + _columnWidths.Sum();
        _totalHeight = ColumnHeaderHeight + sheet.RowCount * CellHeight;

        return new Size(_totalWidth, _totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        return new Size(
            Math.Max(finalSize.Width, _totalWidth),
            Math.Max(finalSize.Height, _totalHeight));
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var sheet = SheetData;
        if (sheet == null || sheet.RowCount == 0 || sheet.ColumnCount == 0)
        {
            RenderEmptyState(context);
            return;
        }

        // Ensure column widths are computed
        if (_columnWidths.Length != sheet.ColumnCount)
            ComputeColumnWidths(Bounds.Width);

        // Determine visible viewport from ScrollViewer parent
        var viewport = GetVisibleViewport();

        // Calculate visible row/column ranges
        int firstVisibleRow = Math.Max(0, (int)((viewport.Top - ColumnHeaderHeight) / CellHeight));
        int lastVisibleRow = Math.Min(sheet.RowCount - 1, (int)((viewport.Bottom - ColumnHeaderHeight) / CellHeight));
        GetVisibleColumnRange(viewport.Left, viewport.Right, out int firstVisibleCol, out int lastVisibleCol);

        // Render layers back to front
        RenderCellBackgrounds(context, sheet, firstVisibleRow, lastVisibleRow, firstVisibleCol, lastVisibleCol);
        RenderRegionOverlays(context, sheet);
        RenderPendingSelection(context, sheet);
        RenderDragSelection(context, sheet);
        RenderGridLines(context, sheet, firstVisibleRow, lastVisibleRow, firstVisibleCol, lastVisibleCol);
        RenderCellText(context, sheet, firstVisibleRow, lastVisibleRow, firstVisibleCol, lastVisibleCol);
        RenderColumnHeaders(context, sheet, firstVisibleCol, lastVisibleCol);
        RenderRowHeaders(context, sheet, firstVisibleRow, lastVisibleRow);
        RenderCornerCell(context);
    }

    #region Column Width Calculation

    private void ComputeColumnWidths(double availableWidth)
    {
        var sheet = SheetData;
        if (sheet == null || sheet.ColumnCount == 0)
        {
            _columnWidths = Array.Empty<double>();
            return;
        }

        int colCount = sheet.ColumnCount;
        _columnWidths = new double[colCount];

        // Fit-to-width: distribute available space minus row header width
        double usableWidth = Math.Max(0, availableWidth - RowHeaderWidth);
        double perCol = usableWidth / colCount;
        double clampedWidth = Math.Clamp(perCol, MinCellWidth, MaxCellWidth);

        for (int c = 0; c < colCount; c++)
            _columnWidths[c] = clampedWidth;
    }

    private double GetColumnX(int col)
    {
        double x = RowHeaderWidth;
        for (int c = 0; c < col && c < _columnWidths.Length; c++)
            x += _columnWidths[c];
        return x;
    }

    private void GetVisibleColumnRange(double viewLeft, double viewRight, out int firstCol, out int lastCol)
    {
        firstCol = 0;
        lastCol = _columnWidths.Length - 1;

        double x = RowHeaderWidth;
        for (int c = 0; c < _columnWidths.Length; c++)
        {
            if (x + _columnWidths[c] >= viewLeft)
            {
                firstCol = c;
                break;
            }
            x += _columnWidths[c];
        }

        x = RowHeaderWidth;
        for (int c = 0; c < _columnWidths.Length; c++)
        {
            x += _columnWidths[c];
            if (x > viewRight)
            {
                lastCol = c;
                break;
            }
        }
    }

    #endregion

    #region Viewport

    private Rect GetVisibleViewport()
    {
        // Walk up to find the ScrollViewer and get its viewport
        var parent = this.Parent;
        while (parent != null)
        {
            if (parent is ScrollViewer sv)
            {
                var offset = sv.Offset;
                return new Rect(offset.X, offset.Y, sv.Viewport.Width, sv.Viewport.Height);
            }
            parent = (parent as Control)?.Parent;
        }

        // Fallback: render everything
        return new Rect(0, 0, _totalWidth, _totalHeight);
    }

    #endregion

    #region Rendering

    private void RenderEmptyState(DrawingContext context)
    {
        var text = CreateFormattedText("No sheet data", 12, false);
        var secondaryText = TryFindBrush("SecondaryText") ?? Brushes.Gray;
        context.DrawText(text, new Point(
            (Bounds.Width - text.Width) / 2,
            (Bounds.Height - text.Height) / 2));
    }

    private void RenderCornerCell(DrawingContext context)
    {
        var headerBg = TryFindBrush("TertiaryBackground") ?? Brushes.LightGray;
        var borderPen = CreateGridPen();
        var rect = new Rect(0, 0, RowHeaderWidth, ColumnHeaderHeight);
        context.DrawRectangle(headerBg, borderPen, rect);
    }

    private void RenderColumnHeaders(DrawingContext context, SASheetData sheet, int firstCol, int lastCol)
    {
        var headerBg = TryFindBrush("TertiaryBackground") ?? Brushes.LightGray;
        var borderPen = CreateGridPen();
        var textBrush = TryFindBrush("PrimaryText") ?? Brushes.Black;

        for (int c = firstCol; c <= lastCol && c < sheet.ColumnCount; c++)
        {
            double x = GetColumnX(c);
            var rect = new Rect(x, 0, _columnWidths[c], ColumnHeaderHeight);
            context.DrawRectangle(headerBg, borderPen, rect);

            // Column letter (A, B, C, ..., AA, AB, ...)
            string label = GetColumnLetter(c);
            var text = CreateFormattedText(label, 11, true);
            context.DrawText(text, new Point(
                x + (_columnWidths[c] - text.Width) / 2,
                (ColumnHeaderHeight - text.Height) / 2));
        }
    }

    private void RenderRowHeaders(DrawingContext context, SASheetData sheet, int firstRow, int lastRow)
    {
        var headerBg = TryFindBrush("TertiaryBackground") ?? Brushes.LightGray;
        var borderPen = CreateGridPen();

        for (int r = firstRow; r <= lastRow && r < sheet.RowCount; r++)
        {
            double y = ColumnHeaderHeight + r * CellHeight;
            var rect = new Rect(0, y, RowHeaderWidth, CellHeight);
            context.DrawRectangle(headerBg, borderPen, rect);

            // 1-based row number for display
            string label = (r + 1).ToString();
            var text = CreateFormattedText(label, 11, false);
            context.DrawText(text, new Point(
                RowHeaderWidth - text.Width - CellTextPadding,
                y + (CellHeight - text.Height) / 2));
        }
    }

    private void RenderCellBackgrounds(DrawingContext context, SASheetData sheet,
        int firstRow, int lastRow, int firstCol, int lastCol)
    {
        var dataBg = TryFindBrush("MainBackground") ?? Brushes.White;
        var emptyBg = TryFindBrush("SecondaryBackground") ?? Brushes.WhiteSmoke;
        var headerRowBg = TryFindBrush("SelectedBackground") ?? Brushes.AliceBlue;

        for (int r = firstRow; r <= lastRow && r < sheet.RowCount; r++)
        {
            double y = ColumnHeaderHeight + r * CellHeight;
            bool isHeader = sheet.IsHeaderRow(r);

            for (int c = firstCol; c <= lastCol && c < sheet.ColumnCount; c++)
            {
                double x = GetColumnX(c);
                var rect = new Rect(x, y, _columnWidths[c], CellHeight);

                IBrush bg;
                if (isHeader)
                    bg = headerRowBg;
                else
                {
                    var cellValue = sheet.GetCellValue(r, c);
                    bg = cellValue.IsEmpty ? emptyBg : dataBg;
                }

                context.DrawRectangle(bg, null, rect);
            }
        }
    }

    private void RenderGridLines(DrawingContext context, SASheetData sheet,
        int firstRow, int lastRow, int firstCol, int lastCol)
    {
        var gridPen = CreateGridPen();

        // Horizontal lines
        for (int r = firstRow; r <= lastRow + 1 && r <= sheet.RowCount; r++)
        {
            double y = ColumnHeaderHeight + r * CellHeight;
            double xStart = GetColumnX(firstCol);
            double xEnd = lastCol < _columnWidths.Length
                ? GetColumnX(lastCol) + _columnWidths[lastCol]
                : _totalWidth;
            context.DrawLine(gridPen, new Point(xStart, y), new Point(xEnd, y));
        }

        // Vertical lines
        for (int c = firstCol; c <= lastCol + 1 && c <= sheet.ColumnCount; c++)
        {
            double x = GetColumnX(c);
            double yStart = ColumnHeaderHeight + firstRow * CellHeight;
            double yEnd = ColumnHeaderHeight + (lastRow + 1) * CellHeight;
            context.DrawLine(gridPen, new Point(x, yStart), new Point(x, yEnd));
        }
    }

    private void RenderCellText(DrawingContext context, SASheetData sheet,
        int firstRow, int lastRow, int firstCol, int lastCol)
    {
        for (int r = firstRow; r <= lastRow && r < sheet.RowCount; r++)
        {
            double y = ColumnHeaderHeight + r * CellHeight;
            bool isHeader = sheet.IsHeaderRow(r);

            for (int c = firstCol; c <= lastCol && c < sheet.ColumnCount; c++)
            {
                string cellText;
                if (isHeader)
                {
                    cellText = c < sheet.ColumnNames.Length ? sheet.ColumnNames[c] : "";
                }
                else
                {
                    var cellValue = sheet.GetCellValue(r, c);
                    if (cellValue.IsEmpty) continue;
                    cellText = cellValue.ToString();
                }

                if (string.IsNullOrEmpty(cellText)) continue;

                double x = GetColumnX(c);
                double maxTextWidth = _columnWidths[c] - CellTextPadding * 2;
                if (maxTextWidth <= 0) continue;

                var text = CreateFormattedText(cellText, 11, isHeader, maxTextWidth);
                double textY = y + (CellHeight - text.Height) / 2;
                double textX = x + CellTextPadding;

                // Clip to cell bounds
                using (context.PushClip(new Rect(x, y, _columnWidths[c], CellHeight)))
                {
                    context.DrawText(text, new Point(textX, textY));
                }
            }
        }
    }

    private void RenderRegionOverlays(DrawingContext context, SASheetData sheet)
    {
        var activeRegion = ActiveRegion;
        if (activeRegion == null) return;

        int startRow = activeRegion.HeaderStartRow ?? activeRegion.DataStartRow;
        int endRow = activeRegion.DataEndRow ?? (sheet.RowCount - 1);
        int startCol = activeRegion.StartColumn ?? 0;
        int endCol = activeRegion.EndColumn ?? (sheet.ColumnCount - 1);

        double x = GetColumnX(startCol);
        double y = ColumnHeaderHeight + startRow * CellHeight;
        double width = GetColumnX(endCol) + (endCol < _columnWidths.Length ? _columnWidths[endCol] : 0) - x;
        double height = (endRow - startRow + 1) * CellHeight;

        var overlayBrush = new SolidColorBrush(Color.FromArgb(40, 255, 107, 53));
        var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 255, 107, 53)), 2);
        context.DrawRectangle(overlayBrush, borderPen, new Rect(x, y, width, height));
    }

    /// <summary>
    /// Renders the confirmed selection (after drag release) with a dashed border.
    /// Visible while the creation panel is shown.
    /// </summary>
    private void RenderPendingSelection(DrawingContext context, SASheetData sheet)
    {
        var selection = SelectionRegion;
        if (selection == null || _isDragging) return;

        int startRow = selection.HeaderStartRow ?? selection.DataStartRow;
        int endRow = selection.DataEndRow ?? startRow;
        int startCol = selection.StartColumn ?? 0;
        int endCol = selection.EndColumn ?? startCol;

        double x = GetColumnX(startCol);
        double y = ColumnHeaderHeight + startRow * CellHeight;
        double width = GetColumnX(endCol) + (endCol < _columnWidths.Length ? _columnWidths[endCol] : 0) - x;
        double height = (endRow - startRow + 1) * CellHeight;

        var selectionFill = new SolidColorBrush(Color.FromArgb(30, 255, 107, 53));
        var selectionBorder = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 107, 53)), 2,
            new DashStyle(new[] { 4.0, 2.0 }, 0));
        context.DrawRectangle(selectionFill, selectionBorder, new Rect(x, y, width, height));
    }

    #endregion

    private void RenderDragSelection(DrawingContext context, SASheetData sheet)
    {
        if (!_isDragging) return;

        int minRow = Math.Min(_dragStartRow, _dragCurrentRow);
        int maxRow = Math.Max(_dragStartRow, _dragCurrentRow);
        int minCol = Math.Min(_dragStartCol, _dragCurrentCol);
        int maxCol = Math.Max(_dragStartCol, _dragCurrentCol);

        // Clamp to sheet bounds
        minRow = Math.Clamp(minRow, 0, sheet.RowCount - 1);
        maxRow = Math.Clamp(maxRow, 0, sheet.RowCount - 1);
        minCol = Math.Clamp(minCol, 0, sheet.ColumnCount - 1);
        maxCol = Math.Clamp(maxCol, 0, sheet.ColumnCount - 1);

        double x = GetColumnX(minCol);
        double y = ColumnHeaderHeight + minRow * CellHeight;
        double width = GetColumnX(maxCol) + (maxCol < _columnWidths.Length ? _columnWidths[maxCol] : 0) - x;
        double height = (maxRow - minRow + 1) * CellHeight;

        var selectionFill = new SolidColorBrush(Color.FromArgb(50, 255, 107, 53)); // AccentOrange 20%
        var selectionBorder = new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 107, 53)), 2,
            new DashStyle(new[] { 4.0, 2.0 }, 0));

        context.DrawRectangle(selectionFill, selectionBorder, new Rect(x, y, width, height));
    }

    #region Pointer Events (Drag Selection)

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (IsReadOnly || SheetData == null) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var pos = e.GetPosition(this);

        // Check if near active region bottom edge → start resize (only in edit mode)
        if (IsEditMode && ActiveRegion != null && IsNearActiveRegionBottomEdge(pos))
        {
            _isResizing = true;
            _resizeStartRow = ActiveRegion.DataEndRow ?? (SheetData.RowCount - 1);
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (!HitTestCell(pos, out int row, out int col)) return;

        // Starting a new drag clears the active region
        if (ActiveRegion != null)
        {
            ActiveRegion = null;
            InvalidateVisual();
        }

        _isDragging = true;
        _dragStartRow = row;
        _dragStartCol = col;
        _dragCurrentRow = row;
        _dragCurrentCol = col;

        e.Pointer.Capture(this);
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var pos = e.GetPosition(this);

        if (_isResizing && SheetData != null && ActiveRegion != null)
        {
            // Calculate new row from pointer position
            int newRow = (int)((pos.Y - ColumnHeaderHeight) / CellHeight);
            newRow = Math.Clamp(newRow, ActiveRegion.DataStartRow, SheetData.RowCount - 1);

            if (newRow != (ActiveRegion.DataEndRow ?? SheetData.RowCount - 1))
            {
                ActiveRegion = ActiveRegion with { DataEndRow = newRow };
                InvalidateVisual();
            }
            return;
        }

        if (_isDragging && SheetData != null)
        {
            HitTestCell(pos, out int row, out int col);

            // Clamp to sheet bounds
            row = Math.Clamp(row, 0, SheetData.RowCount - 1);
            col = Math.Clamp(col, 0, SheetData.ColumnCount - 1);

            if (row != _dragCurrentRow || col != _dragCurrentCol)
            {
                _dragCurrentRow = row;
                _dragCurrentCol = col;
                InvalidateVisual();
            }
            return;
        }

        // Cursor hint: change to resize cursor near bottom edge (only in edit mode)
        if (IsEditMode && ActiveRegion != null && !IsReadOnly && IsNearActiveRegionBottomEdge(pos))
        {
            Cursor = new Cursor(StandardCursorType.SizeNorthSouth);
        }
        else
        {
            Cursor = Cursor.Default;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isResizing)
        {
            _isResizing = false;
            e.Pointer.Capture(null);

            if (ActiveRegion != null)
            {
                RegionResizeCompleted?.Invoke(this, ActiveRegion);
            }
            return;
        }

        if (!_isDragging) return;

        _isDragging = false;
        e.Pointer.Capture(null);

        var sheet = SheetData;
        if (sheet == null) return;

        int minRow = Math.Clamp(Math.Min(_dragStartRow, _dragCurrentRow), 0, sheet.RowCount - 1);
        int maxRow = Math.Clamp(Math.Max(_dragStartRow, _dragCurrentRow), 0, sheet.RowCount - 1);
        int minCol = Math.Clamp(Math.Min(_dragStartCol, _dragCurrentCol), 0, sheet.ColumnCount - 1);
        int maxCol = Math.Clamp(Math.Max(_dragStartCol, _dragCurrentCol), 0, sheet.ColumnCount - 1);

        // Require at least 2 rows (1 header + 1 data)
        if (maxRow - minRow < 1)
        {
            SelectionRegion = null;
            InvalidateVisual();
            return;
        }

        // First selected row = header, rest = data
        SelectionRegion = new DataRegion
        {
            Name = "",
            HeaderStartRow = minRow,
            HeaderEndRow = minRow,
            DataStartRow = minRow + 1,
            DataEndRow = maxRow,
            StartColumn = minCol,
            EndColumn = maxCol,
            IsAutoDetected = false
        };

        InvalidateVisual();
    }

    /// <summary>
    /// Convert pixel position to (row, col). Returns false if outside the data area.
    /// </summary>
    private bool HitTestCell(Point pos, out int row, out int col)
    {
        row = (int)((pos.Y - ColumnHeaderHeight) / CellHeight);
        col = 0;

        if (pos.X < RowHeaderWidth || pos.Y < ColumnHeaderHeight)
        {
            // In header area — still compute row/col for drag clamping
            row = Math.Max(0, row);
            return false;
        }

        double x = RowHeaderWidth;
        for (int c = 0; c < _columnWidths.Length; c++)
        {
            if (pos.X < x + _columnWidths[c])
            {
                col = c;
                return true;
            }
            x += _columnWidths[c];
        }

        col = _columnWidths.Length - 1;
        return true;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Returns true if the pointer position is within ResizeHitZone pixels
    /// of the active region's bottom edge.
    /// </summary>
    private bool IsNearActiveRegionBottomEdge(Point pos)
    {
        var region = ActiveRegion;
        if (region == null || SheetData == null) return false;

        int endRow = region.DataEndRow ?? (SheetData.RowCount - 1);
        double bottomEdgeY = ColumnHeaderHeight + (endRow + 1) * CellHeight;

        // Check horizontal bounds too
        int startCol = region.StartColumn ?? 0;
        int endCol = region.EndColumn ?? (SheetData.ColumnCount - 1);
        double leftX = GetColumnX(startCol);
        double rightX = GetColumnX(endCol) + (endCol < _columnWidths.Length ? _columnWidths[endCol] : 0);

        return Math.Abs(pos.Y - bottomEdgeY) <= ResizeHitZone
            && pos.X >= leftX && pos.X <= rightX;
    }

    private static string GetColumnLetter(int colIndex)
    {
        string result = "";
        int col = colIndex;
        do
        {
            result = (char)('A' + col % 26) + result;
            col = col / 26 - 1;
        } while (col >= 0);
        return result;
    }

    private Pen CreateGridPen()
    {
        var borderBrush = TryFindBrush("BorderColor") ?? Brushes.LightGray;
        return new Pen(borderBrush, 0.5);
    }

    private FormattedText CreateFormattedText(string text, double fontSize, bool bold, double maxWidth = double.PositiveInfinity)
    {
        var textBrush = TryFindBrush("PrimaryText") ?? Brushes.Black;
        var typeface = new Typeface(FontFamily.Default,
            FontStyle.Normal,
            bold ? FontWeight.SemiBold : FontWeight.Normal);

        return new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, typeface, fontSize, textBrush)
        {
            MaxTextWidth = maxWidth > 0 ? maxWidth : double.PositiveInfinity,
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis
        };
    }

    private IBrush? TryFindBrush(string key)
    {
        if (this.TryFindResource(key, this.ActualThemeVariant, out var resource) && resource is IBrush brush)
            return brush;
        return null;
    }

    #endregion
}
