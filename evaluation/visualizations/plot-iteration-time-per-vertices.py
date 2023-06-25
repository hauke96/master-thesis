#!/bin/python

'''
Plots the iteration time of the "iteration_time" column of the given dataset.

Parameters: {file-filter}

Example: ./plot-iteration-time-per-vertices.py "../results/pattern-based-rectangles/pattern_*x*_performance_GenerateGraph.csv"
'''

import common
import os
import sys
import seaborn as sns
import matplotlib.pyplot as plt

common.check_args(1)

dataset_filter=sys.argv[1]
title="HybridVisibilityGraph generation"
dataset=common.load_dataset(dataset_filter)

common.init_seaborn()

#
# Plot absolute numbers
#
dataset["iteration_time_s"]=dataset["iteration_time"]/1000

fig_abs, ax_abs = plt.subplots()
plot=common.create_lineplot(
	dataset,
	ycol="iteration_time_s",
	ylabel="Time in s",
	ax=ax_abs,
	#title,
)
plot.ticklabel_format(style='plain', axis='y')

common.save_to_file(fig_abs, os.path.basename(__file__) + "_absolute")

#
# Plot relative numbers
#
dataset["iteration_time_rel"]=dataset["iteration_time"]/dataset["obstacle_vertices_input"]

fig_rel, ax_rel= plt.subplots()
plot=common.create_lineplot(
	dataset,
	ax=ax_rel,
	ycol="iteration_time_rel",
	ylabel="Time in ms per vertex",
	#title,
)

common.save_to_file(fig_rel, os.path.basename(__file__) + "_per-vertex")
