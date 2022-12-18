set terminal png size 1000,600
set output "routing.png"

set datafile separator ','
set logscale y 2
set style data lines
#set key off

set yrange [1:512]

set xlabel "route length in km"
set ylabel "calculation time in ms"

plot \
    "5x5_performance_Routing.csv" using ($2/1000):3:4:5 title "Routing times with 1525 vertices" with yerrorbars,\
    "" using ($2/1000):3 smooth unique notitle with lines,\
    "10x10_performance_Routing.csv" using ($2/1000):3:4:5 title "Routing times with 6100 vertices" with yerrorbars,\
    "" using ($2/1000):3 smooth unique notitle with lines,\
    "20x20_performance_Routing.csv" using ($2/1000):3:4:5 title "Routing times with 24400 vertices" with yerrorbars,\
    "" using ($2/1000):3 smooth unique notitle with lines
