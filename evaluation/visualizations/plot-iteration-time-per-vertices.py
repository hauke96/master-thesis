#!/bin/python

'''
Plots the iteration time of the "iteration_time" column of the given dataset.

Parameters: {file-filter}

Example: ./plot-iteration-time-per-vertices.py "../results/pattern-based-rectangles/pattern_*x*_performance_GenerateGraph.csv"
'''

import common
import os
import sys

common.check_args(1)

dataset_filter=sys.argv[1]
title="HybridVisibilityGraph generation"
dataset=common.load_dataset(dataset_filter)

common.init_seaborn(width=6, height=4, dpi=120)

plot=common.create_lineplot(
	dataset,
	#title,
)

common.save_to_file(plot.get_figure(), os.path.basename(__file__))
