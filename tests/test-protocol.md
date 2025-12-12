# 🧪 Test Protocol

## **It is used to run complex tests (requiring user interaction) in a controlled and reproducible way**

---

## Memory Profiling Test

### Prerequisites

- Test files ready (same files every test run)
- `dotnet-dump` tool installed (`dotnet tool install -g dotnet-dump`)
- SheetAtlas built in Debug mode

### Execution Steps

1. **Start clean**

   ```bash
   # Kill any running SheetAtlas instances
   pkill -f "SheetAtlas.UI.Avalonia"

   # Start app
   dotnet run --project src/SheetAtlas.UI.Avalonia
   ```

2. **Wait for app startup**
   - Wait ~10 seconds for complete initialization
   - Verify app window is visible and responsive

3. **Capture baseline dump**

   ```bash
   # Find PID
   PID=$(pgrep -f "SheetAtlas.UI.Avalonia/bin" | head -1)

   # Capture baseline
   dotnet-dump collect -p $PID -o tests/dumps/baseline.dump
   ```

4. **Load test files**
   - Load predetermined test files via UI
   - Wait for all files to finish loading (UI responsive)
   - **Do not interact** with app after loading

5. **Wait for memory stabilization**
   - Wait 5 seconds
   - Check memory: `ps -p $PID -o rss=,vsz=`

6. **Capture post-load dump**

   ```bash
   dotnet-dump collect -p $PID -o tests/dumps/post-load.dump
   ```

7. **Close all files**
   - Close all loaded files via UI
   - Wait for UI to update

8. **Wait for garbage collection**
   - Wait 10 seconds for automatic GC

9. **Capture post-cleanup dump**

   ```bash
   dotnet-dump collect -p $PID -o tests/dumps/post-cleanup.dump
   ```

10. **Terminate app**

    ```bash
    kill $PID
    ```

### Analysis

Analyze dumps:

```bash
cd tests/dumps

# Compare heap sizes
echo "=== Baseline ==="
echo "dumpheap -stat" | dotnet-dump analyze baseline.dump 2>&1 | tail -5

echo "=== Post-Load ==="
echo "dumpheap -stat" | dotnet-dump analyze post-load.dump 2>&1 | tail -5

echo "=== Post-Cleanup ==="
echo "dumpheap -stat" | dotnet-dump analyze post-cleanup.dump 2>&1 | tail -5
```

---

## Notes

- **Same test files**: Use identical files across test runs for comparability
- **No interaction**: Don't touch UI during wait periods to avoid memory fluctuations
- **Timing matters**: Respect wait times for GC to stabilize
- **Fresh start**: Always start with clean app instance

---

## Troubleshooting

**Dump collection fails**:

- Verify PID is correct: `ps aux | grep SheetAtlas`
- Check permissions: dumps require same user as process

**Memory doesn't stabilize**:

- Wait longer (up to 30 seconds)
- Check for background tasks in app

**Results inconsistent**:

- Verify same test files used
- Check system load: `top` (high CPU/mem affects results)
- Close other applications during test

---

*Location: `sheet-atlas/tests/test-protocol.md`*
