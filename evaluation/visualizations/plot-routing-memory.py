#!/bin/python

'''
Plots the memory usage (y) of routing requests relative to their length (x). This produces multiple plots on top of each other.

Parameters: {file-filter}

Example: ./plot-routing-memory.py "../results/pattern-based-rectangles/pattern_*x*_performance_Routing.csv"
'''

import common
import os
import sys

common.check_args(1)

dataset_filter=sys.argv[1]
title="Routing - Memory usage"
dataset=common.load_dataset(dataset_filter)
dataset["distance_beeline"]=dataset["distance_beeline"] / 1000

# Convert bytes to MiB
mem_col="avg_mem_after"
mem_col_label="Avgerage RAM usage in MiB"
#mem_col="max_mem"
#mem_col_label="Maximum memory usage in MiB"

dataset[mem_col]=dataset[mem_col] / 1024 / 1024

common.init_seaborn(
	format="large",
	#palette="custom_blue-red"
)

plot=common.create_lineplot(
	dataset,
	#title,
	xcol="distance_beeline",
	xlabel="Beeline distance in km",
	ycol=mem_col,
	ylabel=mem_col_label,
	hue="obstacle_vertices_input",
)
common.set_numeric_legend(plot, "Amount vertices", dataset["obstacle_vertices_input"])

common.save_to_file(plot.get_figure(), os.path.basename(__file__))
