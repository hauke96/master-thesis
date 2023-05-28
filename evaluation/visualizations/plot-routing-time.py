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

common.check_args(1)

dataset_filter=sys.argv[1]
title="Routing - Durations"
dataset=common.load_dataset(dataset_filter)
dataset["distance_beeline"]=dataset["distance_beeline"] / 1000

common.init_seaborn(
	width=6,
	height=4,
	dpi=120,
)

plot=common.create_lineplot(
	dataset,
	title,
	xcol="distance_beeline",
	xlabel="Beeline distance in km",
	ycol="avg_time",
	ylabel="Average routing time in ms",
	hue="total_vertices",
)

sns.move_legend(
	plot,
	"center left",
	bbox_to_anchor=(1.025, 0.5),
	title_fontsize=common.fontsize_small,
	fontsize=common.fontsize_small,
	title='Vertex count'
)

common.save_to_file(plot.get_figure(), os.path.basename(__file__))
