#!/bin/python

'''
Plots the routing time per meter needed based on the number of input vertices.

Parameters: {file-filter} {title}

Example: ./plot-routing-time-per-length.py "../results/pattern-based-rectangles/pattern_*x*_performance_Routing.csv" "Routing time per meter (rectangle dataset)"
'''

import common
import os
import sys

common.check_args(2)

dataset_filter=sys.argv[1]
title=sys.argv[2]
dataset=common.load_dataset(dataset_filter, title)

time_per_distance=dataset["avg_time"] / dataset["distance_beeline"]
dataset["time_per_distance"]=time_per_distance

common.init_seaborn()

plot=common.create_lineplot(
	dataset,
	title,
	ycol="time_per_distance",
	ylabel="Time per meter"
)

common.save_to_file(plot.get_figure(), os.path.basename(__file__))
