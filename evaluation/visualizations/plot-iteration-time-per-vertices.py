#!/bin/python

'''
Plots the iteration time of the "iteration_time" column of the given dataset.

Parameters: {file-filter} {title}

Example: ./plot-iteration-time-per-vertices.py "../results/pattern-based-rectangles/pattern_*x*_performance_GenerateGraph.csv" "HybridVisibilityGraph generation"
'''

import common
import os
import sys

common.check_args(2)

dataset_filter=sys.argv[1]
title=sys.argv[2]
dataset=common.load_dataset(dataset_filter, title)

common.init_seaborn(width=6, height=4, dpi=120)

#dataset['memory'] = dataset['memory'] / 1000000

plot=common.create_lineplot(
	dataset,
	title,
	#ycol='memory',
	#ylabel='Memory usage in MB'
)

common.save_to_file(plot.get_figure(), os.path.basename(__file__), "png")
