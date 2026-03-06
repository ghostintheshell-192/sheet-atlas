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

    // Merged cell lookup: every cell in a merge maps to its MergedRange
    private Dictionary<(int Row, int Col), MergedRange>? _mergeLookup;

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

    public static readonly StyledProperty<bool> IsSelectingHeaderProperty =
        AvaloniaProperty.Register<SheetGridCanvas, bool>(nameof(IsSelectingHeader),
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

    /// <summary>
    /// When true, drag selects header rows within the existing selection instead of a new area.
    /// </summary>
    public bool IsSelectingHeader
    {
        get => GetValue(IsSelectingHeaderProperty);
        set => SetValue(IsSelectingHeaderProperty, value);
    }

    #endregion

    static SheetGridCanvas()
    {
        AffectsRender<SheetGridCanvas>(SheetDataProperty, RegionsProperty, SelectionRegionProperty, ActiveRegionProperty, IsSelectingHeaderProperty);
        AffectsMeasure<SheetGridCanvas>(SheetDataProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var sheet = SheetData;
        if (sheet == null || sheet.RowCount == 0 || sheet.ColumnCount == 0)
            return new Size(200, 50);

        ComputeColumnWidths(availableSize.Width);
        _totalWidth = RowHeaderWidth + _columnWidths.Sum();
        _totalHeight = ColumnHeaderHeight + (sheet.OriginRow + sheet.RowCount) * CellHeight;

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

        // Ensure column widths are computed (total display columns = origin offset + data columns)
        int totalDisplayCols = sheet.OriginColumn + sheet.ColumnCount;
        int totalDisplayRows = sheet.OriginRow + sheet.RowCount;
        if (_columnWidths.Length != totalDisplayCols)
            ComputeColumnWidths(Bounds.Width);

        BuildMergeMap(sheet);

        // Determine visible viewport from ScrollViewer parent
        var viewport = GetVisibleViewport();

        // Calculate visible row/column ranges (in display space: 0=A, 0=row1)
        int firstVisibleRow = Math.Max(0, (int)((viewport.Top - ColumnHeaderHeight) / CellHeight));
        int lastVisibleRow = Math.Min(totalDisplayRows - 1, (int)((viewport.Bottom - ColumnHeaderHeight) / CellHeight));
        GetVisibleColumnRange(viewport.Left, viewport.Right, out int firstVisibleCol, out int lastVisibleCol);

        // Render layers back to front
        RenderCellBackgrounds(context, sheet, firstVisibleRow, lastVisibleRow, firstVisibleCol, lastVisibleCol);
        RenderRegionOverlays(context, sheet);
        RenderPendingSelection(context, sheet);
        RenderDragSelection(context, sheet);
        RenderGridLines(context, sheet, firstVisibleRow, lastVisibleRow, firstVisibleCol, lastVisibleCol);
        RenderMergedCells(context, sheet, firstVisibleRow, lastVisibleRow, firstVisibleCol, lastVisibleCol);
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

        int colCount = sheet.OriginColumn + sheet.ColumnCount;
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

    private void BuildMergeMap(SASheetData sheet)
    {
        if (sheet.MergedCells.Count == 0)
        {
            _mergeLookup = null;
            return;
        }

        _mergeLookup = new Dictionary<(int, int), MergedRange>();
        foreach (var range in sheet.MergedCells.Values)
        {
            for (int r = range.StartRow; r <= range.EndRow; r++)
                for (int c = range.StartCol; c <= range.EndCol; c++)
                    _mergeLookup[(r, c)] = range;
        }
    }

    /// <summary>
    /// Returns true if this cell is part of a merge but is NOT the top-left anchor.
    /// </summary>
    private bool IsSecondaryMergedCell(int localRow, int localCol)
    {
        if (_mergeLookup == null) return false;
        if (!_mergeLookup.TryGetValue((localRow, localCol), out var range)) return false;
        return range.StartRow != localRow || range.StartCol != localCol;
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

        for (int c = firstCol; c <= lastCol && c < _columnWidths.Length; c++)
        {
            double x = GetColumnX(c);
            var rect = new Rect(x, 0, _columnWidths[c], ColumnHeaderHeight);
            context.DrawRectangle(headerBg, borderPen, rect);

            // Display index IS the Excel column index (canvas starts from A=0)
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

        for (int r = firstRow; r <= lastRow && r < sheet.OriginRow + sheet.RowCount; r++)
        {
            double y = ColumnHeaderHeight + r * CellHeight;
            var rect = new Rect(0, y, RowHeaderWidth, CellHeight);
            context.DrawRectangle(headerBg, borderPen, rect);

            // Display index IS the 0-based Excel row index; label is 1-based
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

        for (int r = firstRow; r <= lastRow && r < sheet.OriginRow + sheet.RowCount; r++)
        {
            double y = ColumnHeaderHeight + r * CellHeight;
            int localRow = r - sheet.OriginRow;
            bool isHeader = localRow >= 0 && sheet.IsHeaderRow(localRow);

            for (int c = firstCol; c <= lastCol && c < sheet.OriginColumn + sheet.ColumnCount; c++)
            {
                int localCol = c - sheet.OriginColumn;

                // Skip secondary merged cells — the top-left cell will cover them
                if (localRow >= 0 && localCol >= 0 && IsSecondaryMergedCell(localRow, localCol))
                    continue;

                double x = GetColumnX(c);
                var rect = new Rect(x, y, _columnWidths[c], CellHeight);

                IBrush bg;
                if (localRow < 0 || localCol < 0)
                    bg = emptyBg;
                else if (isHeader)
                    bg = headerRowBg;
                else
                {
                    var cellValue = sheet.GetCellValue(localRow, localCol);
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

        int totalDisplayRows = sheet.OriginRow + sheet.RowCount;
        int totalDisplayCols = sheet.OriginColumn + sheet.ColumnCount;

        // Horizontal lines
        for (int r = firstRow; r <= lastRow + 1 && r <= totalDisplayRows; r++)
        {
            double y = ColumnHeaderHeight + r * CellHeight;
            double xStart = GetColumnX(firstCol);
            double xEnd = lastCol < _columnWidths.Length
                ? GetColumnX(lastCol) + _columnWidths[lastCol]
                : _totalWidth;
            context.DrawLine(gridPen, new Point(xStart, y), new Point(xEnd, y));
        }

        // Vertical lines
        for (int c = firstCol; c <= lastCol + 1 && c <= totalDisplayCols; c++)
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
        for (int r = firstRow; r <= lastRow && r < sheet.OriginRow + sheet.RowCount; r++)
        {
            double y = ColumnHeaderHeight + r * CellHeight;
            int localRow = r - sheet.OriginRow;
            if (localRow < 0) continue; // empty origin rows have no text

            bool isHeader = sheet.IsHeaderRow(localRow);

            for (int c = firstCol; c <= lastCol && c < sheet.OriginColumn + sheet.ColumnCount; c++)
            {
                int localCol = c - sheet.OriginColumn;
                if (localCol < 0) continue;

                // Skip merged cells — they are rendered by RenderMergedCells
                if (IsSecondaryMergedCell(localRow, localCol)) continue;
                if (_mergeLookup != null && _mergeLookup.ContainsKey((localRow, localCol))) continue;

                // Always use actual cell values — ColumnNames may contain auto-generated
                // names for empty cells (e.g. "Column_7") that don't exist in the original file.
                var cellValue = sheet.GetCellValue(localRow, localCol);
                if (cellValue.IsEmpty) continue;
                string cellText = cellValue.ToString();

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

    /// <summary>
    /// Renders merged cells as spanning rectangles, drawn after grid lines
    /// so the fill covers internal grid lines within the merge.
    /// </summary>
    private void RenderMergedCells(DrawingContext context, SASheetData sheet,
        int firstRow, int lastRow, int firstCol, int lastCol)
    {
        if (_mergeLookup == null) return;

        // Use fully opaque backgrounds so the fill completely covers internal grid lines.
        // Theme brushes like SelectedBackground may have low opacity (e.g. 0.2), which
        // would let grid lines bleed through and break the merged-cell illusion.
        IBrush dataBg = (IBrush?)TryFindOpaqueBrush("MainBackground") ?? Brushes.White;
        IBrush headerRowBg = (IBrush?)TryFindOpaqueBrush("SelectedBackground")
            ?? (IBrush?)TryFindOpaqueBrush("MainBackground")
            ?? Brushes.White;
        var borderBrush = TryFindBrush("BorderColor") ?? Brushes.LightGray;
        var mergeBorderPen = new Pen(borderBrush, 1.0);

        // Track which merges we've already drawn (avoid duplicates when multiple
        // cells of the same merge are in the visible range)
        var rendered = new HashSet<(int, int)>();

        foreach (var range in sheet.MergedCells.Values)
        {
            // Check if this merge overlaps the visible viewport
            int displayStartRow = range.StartRow + sheet.OriginRow;
            int displayEndRow = range.EndRow + sheet.OriginRow;
            int displayStartCol = range.StartCol + sheet.OriginColumn;
            int displayEndCol = range.EndCol + sheet.OriginColumn;

            if (displayEndRow < firstRow || displayStartRow > lastRow) continue;
            if (displayEndCol < firstCol || displayStartCol > lastCol) continue;
            if (!rendered.Add((range.StartRow, range.StartCol))) continue;

            bool isHeader = sheet.IsHeaderRow(range.StartRow);
            IBrush bg = isHeader ? headerRowBg : dataBg;

            double x = GetColumnX(displayStartCol);
            double y = ColumnHeaderHeight + displayStartRow * CellHeight;
            double width = 0;
            for (int c = displayStartCol; c <= displayEndCol && c < _columnWidths.Length; c++)
                width += _columnWidths[c];
            double height = (displayEndRow - displayStartRow + 1) * CellHeight;

            // Opaque fill covers internal grid lines; border marks the merge outline
            var mergedRect = new Rect(x, y, width, height);
            context.DrawRectangle(bg, mergeBorderPen, mergedRect);

            // Draw text from the top-left cell (use actual cell value, not ColumnNames)
            var cellValue = sheet.GetCellValue(range.StartRow, range.StartCol);
            if (cellValue.IsEmpty) continue;
            string cellText = cellValue.ToString();

            if (string.IsNullOrEmpty(cellText)) continue;

            double maxTextWidth = width - CellTextPadding * 2;
            if (maxTextWidth <= 0) continue;

            var text = CreateFormattedText(cellText, 11, isHeader, maxTextWidth);
            // Center text within the merged area (like Excel)
            double textX = x + (width - text.Width) / 2;
            double textY = y + (height - text.Height) / 2;

            using (context.PushClip(mergedRect))
            {
                context.DrawText(text, new Point(textX, textY));
            }
        }
    }

    /// <summary>
    /// Resolves a theme brush and ensures it is fully opaque.
    /// If the brush has sub-1.0 opacity, blends its color with MainBackground
    /// to produce an equivalent opaque color.
    /// </summary>
    private SolidColorBrush? TryFindOpaqueBrush(string resourceKey)
    {
        if (!this.TryFindResource(resourceKey, this.ActualThemeVariant, out var resource)) return null;
        if (resource is not SolidColorBrush brush) return null;

        // Already opaque
        if (brush.Opacity >= 1.0 && brush.Color.A == 255)
            return brush;

        // Blend with MainBackground to produce an opaque equivalent
        var baseBrush = TryFindBrush("MainBackground") as SolidColorBrush;
        var baseColor = baseBrush?.Color ?? Color.FromRgb(13, 17, 23); // #0D1117 fallback

        double alpha = brush.Opacity * (brush.Color.A / 255.0);
        byte r = (byte)(brush.Color.R * alpha + baseColor.R * (1 - alpha));
        byte g = (byte)(brush.Color.G * alpha + baseColor.G * (1 - alpha));
        byte b = (byte)(brush.Color.B * alpha + baseColor.B * (1 - alpha));

        return new SolidColorBrush(Color.FromRgb(r, g, b));
    }

    private void RenderRegionOverlays(DrawingContext context, SASheetData sheet)
    {
        var activeRegion = ActiveRegion;
        if (activeRegion == null) return;

        // DataRegion coordinates are local (SASheetData-space); add origin for display position
        int startRow = (activeRegion.HeaderStartRow ?? activeRegion.DataStartRow) + sheet.OriginRow;
        int endRow = (activeRegion.DataEndRow ?? (sheet.RowCount - 1)) + sheet.OriginRow;
        int startCol = (activeRegion.StartColumn ?? 0) + sheet.OriginColumn;
        int endCol = (activeRegion.EndColumn ?? (sheet.ColumnCount - 1)) + sheet.OriginColumn;

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

        int localStartCol = selection.StartColumn ?? 0;
        int localEndCol = selection.EndColumn ?? localStartCol;
        int startCol = localStartCol + sheet.OriginColumn;
        int endCol = localEndCol + sheet.OriginColumn;

        double x = GetColumnX(startCol);
        double width = GetColumnX(endCol) + (endCol < _columnWidths.Length ? _columnWidths[endCol] : 0) - x;

        if (selection.HeaderStartRow != null)
        {
            // Two-zone rendering: header (blue) + data (orange) + divider
            int headerStart = selection.HeaderStartRow.Value + sheet.OriginRow;
            int headerEnd = (selection.HeaderEndRow ?? selection.HeaderStartRow.Value) + sheet.OriginRow;
            int dataStart = selection.DataStartRow + sheet.OriginRow;
            int dataEnd = (selection.DataEndRow ?? dataStart) + sheet.OriginRow;

            // Header zone (blue)
            double headerY = ColumnHeaderHeight + headerStart * CellHeight;
            double headerHeight = (headerEnd - headerStart + 1) * CellHeight;
            var headerFill = new SolidColorBrush(Color.FromArgb(40, 66, 133, 244));
            var headerBorder = new Pen(new SolidColorBrush(Color.FromArgb(200, 66, 133, 244)), 2,
                new DashStyle(new[] { 4.0, 2.0 }, 0));
            context.DrawRectangle(headerFill, headerBorder, new Rect(x, headerY, width, headerHeight));

            // Data zone (orange)
            if (dataStart <= dataEnd)
            {
                double dataY = ColumnHeaderHeight + dataStart * CellHeight;
                double dataHeight = (dataEnd - dataStart + 1) * CellHeight;
                var dataFill = new SolidColorBrush(Color.FromArgb(20, 255, 107, 53));
                var dataBorder = new Pen(new SolidColorBrush(Color.FromArgb(150, 255, 107, 53)), 1);
                context.DrawRectangle(dataFill, dataBorder, new Rect(x, dataY, width, dataHeight));
            }

            // Divider line between header and data
            double dividerY = ColumnHeaderHeight + (headerEnd + 1) * CellHeight;
            var dividerPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 66, 133, 244)), 2);
            context.DrawLine(dividerPen, new Point(x, dividerY), new Point(x + width, dividerY));
        }
        else
        {
            // Single-zone: whole area in orange (no header selected yet)
            int areaStart = selection.DataStartRow + sheet.OriginRow;
            int areaEnd = (selection.DataEndRow ?? areaStart) + sheet.OriginRow;

            double y = ColumnHeaderHeight + areaStart * CellHeight;
            double height = (areaEnd - areaStart + 1) * CellHeight;

            var selectionFill = new SolidColorBrush(Color.FromArgb(30, 255, 107, 53));
            var selectionBorder = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 107, 53)), 2,
                new DashStyle(new[] { 4.0, 2.0 }, 0));
            context.DrawRectangle(selectionFill, selectionBorder, new Rect(x, y, width, height));
        }
    }

    #endregion

    private void RenderDragSelection(DrawingContext context, SASheetData sheet)
    {
        if (!_isDragging) return;

        int minRow = Math.Min(_dragStartRow, _dragCurrentRow);
        int maxRow = Math.Max(_dragStartRow, _dragCurrentRow);
        int minCol = Math.Min(_dragStartCol, _dragCurrentCol);
        int maxCol = Math.Max(_dragStartCol, _dragCurrentCol);

        // Clamp to data area (drag cannot go into empty origin area)
        minRow = Math.Clamp(minRow, sheet.OriginRow, sheet.OriginRow + sheet.RowCount - 1);
        maxRow = Math.Clamp(maxRow, sheet.OriginRow, sheet.OriginRow + sheet.RowCount - 1);
        minCol = Math.Clamp(minCol, sheet.OriginColumn, sheet.OriginColumn + sheet.ColumnCount - 1);
        maxCol = Math.Clamp(maxCol, sheet.OriginColumn, sheet.OriginColumn + sheet.ColumnCount - 1);

        double x = GetColumnX(minCol);
        double y = ColumnHeaderHeight + minRow * CellHeight;
        double width = GetColumnX(maxCol) + (maxCol < _columnWidths.Length ? _columnWidths[maxCol] : 0) - x;
        double height = (maxRow - minRow + 1) * CellHeight;

        IBrush selectionFill;
        Pen selectionBorder;
        if (IsSelectingHeader)
        {
            selectionFill = new SolidColorBrush(Color.FromArgb(50, 66, 133, 244));
            selectionBorder = new Pen(new SolidColorBrush(Color.FromArgb(220, 66, 133, 244)), 2,
                new DashStyle(new[] { 4.0, 2.0 }, 0));
        }
        else
        {
            selectionFill = new SolidColorBrush(Color.FromArgb(50, 255, 107, 53));
            selectionBorder = new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 107, 53)), 2,
                new DashStyle(new[] { 4.0, 2.0 }, 0));
        }

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

        // In header-selection mode, require an existing selection area
        if (IsSelectingHeader && SelectionRegion == null) return;

        // Starting a new area drag clears the active region
        if (!IsSelectingHeader && ActiveRegion != null)
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
            // newRow is in display space; clamp to data area then convert to local
            int newDisplayRow = (int)((pos.Y - ColumnHeaderHeight) / CellHeight);
            int minDisplayRow = ActiveRegion.DataStartRow + SheetData.OriginRow;
            int maxDisplayRow = SheetData.OriginRow + SheetData.RowCount - 1;
            newDisplayRow = Math.Clamp(newDisplayRow, minDisplayRow, maxDisplayRow);
            int newRow = newDisplayRow - SheetData.OriginRow;

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

            // Clamp to display bounds (HitTestCell returns display-space coords)
            row = Math.Clamp(row, 0, SheetData.OriginRow + SheetData.RowCount - 1);
            col = Math.Clamp(col, 0, SheetData.OriginColumn + SheetData.ColumnCount - 1);

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

        // Drag coords are in display space; clamp to data area then convert to local
        int minDisplayRow = Math.Clamp(Math.Min(_dragStartRow, _dragCurrentRow), sheet.OriginRow, sheet.OriginRow + sheet.RowCount - 1);
        int maxDisplayRow = Math.Clamp(Math.Max(_dragStartRow, _dragCurrentRow), sheet.OriginRow, sheet.OriginRow + sheet.RowCount - 1);
        int minDisplayCol = Math.Clamp(Math.Min(_dragStartCol, _dragCurrentCol), sheet.OriginColumn, sheet.OriginColumn + sheet.ColumnCount - 1);
        int maxDisplayCol = Math.Clamp(Math.Max(_dragStartCol, _dragCurrentCol), sheet.OriginColumn, sheet.OriginColumn + sheet.ColumnCount - 1);

        int minRow = minDisplayRow - sheet.OriginRow;
        int maxRow = maxDisplayRow - sheet.OriginRow;
        int minCol = minDisplayCol - sheet.OriginColumn;
        int maxCol = maxDisplayCol - sheet.OriginColumn;

        if (IsSelectingHeader && SelectionRegion != null)
        {
            // Header-selection mode: clamp drag within existing selection bounds
            var sel = SelectionRegion;
            int areaStart = sel.HeaderStartRow ?? sel.DataStartRow;
            int areaEnd = sel.DataEndRow ?? areaStart;
            int clampedMin = Math.Clamp(minRow, areaStart, areaEnd);
            int clampedMax = Math.Clamp(maxRow, areaStart, areaEnd);

            SelectionRegion = sel with
            {
                HeaderStartRow = clampedMin,
                HeaderEndRow = clampedMax,
                DataStartRow = clampedMax + 1
            };
        }
        else
        {
            // Area-selection mode: whole drag is the region area, no header yet
            SelectionRegion = new DataRegion
            {
                Name = "",
                HeaderStartRow = null,
                HeaderEndRow = null,
                DataStartRow = minRow,
                DataEndRow = maxRow,
                StartColumn = minCol,
                EndColumn = maxCol,
                IsAutoDetected = false
            };
        }

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

        // Convert local region coords to display coords for pixel calculation
        int endRow = (region.DataEndRow ?? (SheetData.RowCount - 1)) + SheetData.OriginRow;
        double bottomEdgeY = ColumnHeaderHeight + (endRow + 1) * CellHeight;

        // Check horizontal bounds too
        int startCol = (region.StartColumn ?? 0) + SheetData.OriginColumn;
        int endCol = (region.EndColumn ?? (SheetData.ColumnCount - 1)) + SheetData.OriginColumn;
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
