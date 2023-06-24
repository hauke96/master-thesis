#!/bin/python

'''
Plots detailed routing times of the given dataset.

Parameters: {file-filter}

Example: ./plot-routing-time-details.py "../results/pattern-based-rectangles/pattern_*x*_performance_RoutingGraph.csv"
'''

import common
import os
import sys
import pandas as pd
import seaborn as sns
import matplotlib.pyplot as plt
from matplotlib import container

common.check_args(1)

dataset_filter=sys.argv[1]

dataset_cols=[
	'astar_avg_time',
	'add_positions_to_graph_avg_time',
	'restore_avg_time',
]
dataset_labels=[
	'A* routing',
	'Connecting\nsource \\& destination\nvertices',
	'Restoring original\ngraph',
]

dataset_raw=common.load_dataset(dataset_filter)
dataset_raw["distance_beeline"]=dataset_raw["distance_beeline"] / 1000
dataset_relevant=dataset_raw[["distance_beeline", "avg_time"] + dataset_cols]
dataset=dataset_relevant.melt('distance_beeline', var_name='aspect', value_name='time')

common.init_seaborn(
	width=7,
	height=4,
	dpi=120,
)

#
# Plot absolute numbers
#

title="Routing - Durations broken down"
fig_abs, ax_abs = plt.subplots()

common.create_lineplot(
	dataset,
	#title,
	xcol="distance_beeline",
	xlabel="Beeline distance in km",
	ycol='time',
	ylabel='Time in ms',
	hue="aspect",
	yscale='log',
	#errorbar=("pi", 50),
	ax=ax_abs
)

handles, labels = ax_abs.get_legend_handles_labels()
handles=[h for h in handles if not isinstance(h, container.ErrorbarContainer)]

sns.move_legend(
	ax_abs,
	"center left",
	bbox_to_anchor=(1.025, 0.5),
	handles=handles,
	labels=["Total routing\ntime"] + dataset_labels,
	title_fontsize=common.fontsize_small,
	fontsize=common.fontsize_small,
	title='Legend',
)

common.save_to_file(fig_abs, os.path.basename(__file__) + "_absolute")

#
# Plot relative numbers
#

title="Routing - Durations relative share"
fig_rel, ax_rel = plt.subplots()

#dataset_raw.reset_index(drop=True, inplace=True)
#dataset_relevant.reset_index(drop=True, inplace=True)
dataset_relevant=dataset_raw[dataset_cols + ["distance_beeline"]]
dataset_relevant[dataset_cols]=dataset_raw[dataset_cols].div(dataset_raw["avg_time"], axis=0)
dataset=dataset_relevant.melt('distance_beeline', var_name='aspect', value_name='time')

common.create_lineplot(
	dataset,
	#title,
	xcol="distance_beeline",
	xlabel="Beeline distance in km",
	ycol='time',
	ylabel='Share of total time',
	hue="aspect",
	#yscale='log',
	ax=ax_rel
)

handles, labels = ax_abs.get_legend_handles_labels()
handles=[h for h in handles if not isinstance(h, container.ErrorbarContainer)]

sns.move_legend(
	ax_rel,
	"center left",
	bbox_to_anchor=(1.025, 0.5),
	handles=handles,
	labels=dataset_labels,
	title_fontsize=common.fontsize_small,
	fontsize=common.fontsize_small,
	title='Legend'
)

common.save_to_file(fig_rel, os.path.basename(__file__) + "_relative")
