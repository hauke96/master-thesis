set terminal png
set output "consolidate-avg-min-max-data.png"

set datafile separator ','
#set logscale xy 10
set style data lines
set key off

# Function trying to describe the CalculateVisibleKnn performance of the pattern-based measurements
# f(x)=x/10 + 0.000025 * (x ** 2.25)

plot \
    "consolidate-avg-min-max-data.csv" using 2:3:4:5 title "Average time (with min/max range)" with yerrorbars,\
    "" using 2:3 notitle with lines,\
    #f(x) title "f(x) = x/10 + 0.000025 * x^{2.25}"
