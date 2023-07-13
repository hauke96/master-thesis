#!/bin/python

'''
Plots the time needed time for routing requests (y) relative to their length (x). This produces multiple plots on top of each other.

Parameters: {file-filter}

Example: ./plot-routing-time.py "../results/pattern-based-rectangles/pattern_*x*_performance_Routing.csv"
'''

import common
import os
import sys
import seaborn as sns
import matplotlib.pyplot as plt

common.check_args(1)

dataset_filter=sys.argv[1]
dataset=common.load_dataset(dataset_filter)
dataset["distance_beeline"]=dataset["distance_beeline"] / 1000

common.init_seaborn()

#
# x = beeline distance
#

common.init_seaborn(format="large_slim")

title="Routing - Durations"
fig, ax=plt.subplots()

common.create_lineplot(
	dataset,
	#title,
	ax=ax,
	xcol="distance_beeline",
	xlabel="Beeline distance in km",
	ycol="avg_time",
	ylabel="Average routing time in ms",
	hue="obstacle_vertices_input",
)

sns.move_legend(
	ax,
	"center left",
	bbox_to_anchor=(1.025, 0.5),
	title_fontsize=common.fontsize_small,
	fontsize=common.fontsize_small,
	title='Vertex count'
)

common.save_to_file(fig, os.path.basename(__file__) + "_distance")

#
# x = beeline distance
#

common.init_seaborn(
	palette="custom_blue-red"
)
title="Routing - Time per input vertex"
fig, ax=plt.subplots()

dataset["avg_time_per_vertex"]=dataset["avg_time"].div(dataset["obstacle_vertices_input"], axis=0)

common.create_scatter_lineplot(
	dataset,
	#title,
	ax=ax,
	xcol="obstacle_vertices_input",
	xlabel="Input obstacle vertices",
	ycol="avg_time_per_vertex",
	ylabel="Average routing time in ms per vertex",
)
#ax.set_xlim(3000, None)
#ax.set_ylim(0.003, 0.013)

#sns.move_legend(
#	ax,
#	"center left",
#	bbox_to_anchor=(1.025, 0.5),
#	title_fontsize=common.fontsize_small,
#	fontsize=common.fontsize_small,
#	title='Distance'
#)

common.save_to_file(fig, os.path.basename(__file__) + "_vertices")
