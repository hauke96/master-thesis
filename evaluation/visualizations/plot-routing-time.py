#!/bin/python

'''
Plots the time needed (y) for routing requests relative to their length (x). This produces multiple plots on top of each other.

Parameters: {file-filter} {title}

Example: ./plot-routing-time.py "../results/pattern-based-rectangles/pattern_*x*_performance_Routing.csv" "Routing time (rectangle dataset)"
'''

import common
import os
import sys
import seaborn as sns

common.check_args(2)

dataset_filter=sys.argv[1]
title=sys.argv[2]
dataset=common.load_dataset(dataset_filter, title)
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
