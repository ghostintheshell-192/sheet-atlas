#!/bin/bash

# Trova il processo REALE (non il wrapper dotnet run)
# Cerca quello con /bin/Debug o /bin/Release nel path
PID=$(pgrep -f "SheetAtlas.UI.Avalonia/bin" | head -1)

if [ -z "$PID" ]; then
    echo "SheetAtlas non è in esecuzione"
    echo "Processi trovati:"
    pgrep -af SheetAtlas
    exit 1
fi

echo "=== Monitoring SheetAtlas REAL PROCESS (PID: $PID) ==="
echo "Time     | RSS (MB) | VSZ (MB) | %MEM"
echo "---------|----------|----------|------"

while kill -0 $PID 2>/dev/null; do
    ps -p $PID -o rss=,vsz=,pmem= 2>/dev/null | awk -v time="$(date +%H:%M:%S)" '{printf "%s | %8.2f | %8.2f | %.1f%%\n", time, $1/1024, $2/1024, $3}'
    sleep 2
done

echo "Process terminated"
