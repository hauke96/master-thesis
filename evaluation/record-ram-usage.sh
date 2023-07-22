#!/bin/bash
i
DELAY=0.09 # 100ms minus a static 10ms offset for determining and writing the measurement

echo "Stop recording with CTRL+C"
read -p "PID to record: " PID

OUT="./ram-usage_$PID.csv"
echo "Write results to $OUT"
echo "Start recording..."

echo "time,kb" > $OUT

while true
do
    sleep $DELAY
    TIME=$(($(date +%s%N)/1000000))
    RAM_USAGE_BYTES=$(ps -o pid,rss ax | grep "\s*$PID\s" | awk '{print $2}')
    if [[ -z $RAM_USAGE_BYTES ]]
    then
        echo "Process $PID ended"
        exit 1
    fi
    echo "$TIME,$RAM_USAGE_BYTES" >> $OUT
done
