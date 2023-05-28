#!/bin/python

'''
Plots detailed import times of the given dataset.

Parameters: {file-filter} {title}

Example: ./plot-iteration-details-per-vertices.py "../results/pattern-based-rectangles/pattern_*x*_performance_GenerateGraph.csv" "Graph generation (rectangle dataset)"
'''

import common
import os
import sys
import pandas as pd
import seaborn as sns
import matplotlib.pyplot as plt

common.check_args(2)

dataset_filter=sys.argv[1]
title=sys.argv[2]

dataset_cols=[
	# Only when yscale='log':
	#'iteration_time'
	'total_vertices',
	'knn_search_time',
	'build_graph_time',
	'get_obstacle_time',
	'merge_road_graph_time',
	'add_poi_attributes_time',
]
dataset_labels=[
	# Only when yscale='log':
	#'Total time'
	'Find all $k$\nnearest visibility\nneighbors',
	'Building the\ngraph from\nvisibility neighbors',
	'Getting and\npreparing all\nobstacles',
	'Merge V-Graph\ninto the\nroad graph',
	'Add attributed\nto POIs',
]

dataset=common.load_dataset(dataset_filter, title)
dataset=dataset[dataset_cols]
dataset=dataset.melt('total_vertices', var_name='aspect', value_name='time')

common.init_seaborn(
	width=7,
	height=4,
	dpi=120,
)

plot=common.create_lineplot(
	dataset,
	title,
	ycol='time',
	ylabel='Time in ms',
	hue="aspect",
	yscale='log'
)

sns.move_legend(
	plot,
	"center left",
	bbox_to_anchor=(1.025, 0.5),
	labels=dataset_labels,
	title_fontsize=common.fontsize_small,
	fontsize=common.fontsize_small,
	title='Legend'
)

common.save_to_file(plot.get_figure(), os.path.basename(__file__), "png")
