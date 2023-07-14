#!/bin/python

'''
Plots the import time ("iteration_time" column) of the given dataset.

Parameters: {file-filter}

Example: ./plot-import-time-per-vertices.py "../results/pattern-based-rectangles/pattern_*x*_performance_GenerateGraph.csv"
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

fig, ax = plt.subplots()
plot=common.create_lineplot(
	dataset,
	ycol="iteration_time_s",
	ylabel="Time in s",
	ax=ax,
	#title,
)
plot.ticklabel_format(style='plain', axis='y')

#common.save_to_file(fig, os.path.basename(__file__) + "_absolute")

#
# Plot relative numbers
#
dataset["iteration_time_rel"]=dataset["iteration_time"]/dataset["obstacle_vertices_input"]

fig, ax= plt.subplots()
plot=common.create_lineplot(
	dataset,
	ax=ax,
	ycol="iteration_time_rel",
	ylabel="Time in ms",
	#title,
)

#common.save_to_file(fig, os.path.basename(__file__) + "_per-vertex")

#
# Plot time increase per vertex for each new vertex. I.e. the processing time of every vertex increases by this amount if a new vertex is added
#
dataset["iteration_time_rel_added"]=dataset["iteration_time_rel"]/dataset["obstacle_vertices_input"]*1000

print(dataset[["obstacle_vertices_input", "iteration_time_rel_added"]].to_string())

fig_added, ax_added= plt.subplots()
plot=common.create_lineplot(
	dataset,
	ax=ax_added,
	ycol="iteration_time_rel_added",
	ylabel="Time in µs",
	scientific_labels=False,
)

#common.save_to_file(fig_added, os.path.basename(__file__) + "_per-vertex-added")
