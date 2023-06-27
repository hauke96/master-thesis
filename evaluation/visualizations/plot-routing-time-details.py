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
	'avg_time',
	'astar_avg_time',
	'add_positions_to_graph_avg_time',
	'restore_avg_time',
]
dataset_labels=[
	"Total routing\ntime",
	'A* routing',
	'Connecting\nsource \\& destination\nvertices',
	'Restoring original\ngraph',
]

dataset_raw=common.load_dataset(dataset_filter)
dataset_raw["distance_beeline"]=dataset_raw["distance_beeline"] / 1000

dataset_ids=dataset_raw["obstacle_vertices_input"].unique();

max_beeline_distance=max(dataset_raw["distance_beeline"]);

#
# Plot absolute numbers
#

for id in dataset_ids:
	dataset_filtered=dataset_raw[dataset_raw["obstacle_vertices_input"] == id];
	dataset_relevant=dataset_filtered[["distance_beeline"] + dataset_cols]
	dataset=dataset_relevant.melt('distance_beeline', var_name='aspect', value_name='time')

	common.init_seaborn(width=440)

	title="Routing - Durations broken down"
	fig, ax = plt.subplots()

	plot=common.create_lineplot(
		dataset,
		#title,
		xcol="distance_beeline",
		xlabel="Beeline distance in km",
		ycol='time',
		ylabel='Time in ms',
		hue="aspect",
		yscale='log',
		#errorbar=("pi", 50),
		ax=ax
	)

	plot.set_xlim(0, max_beeline_distance)

	handles, labels = ax_abs.get_legend_handles_labels()
	handles=[h for h in handles if not isinstance(h, container.ErrorbarContainer)]

	sns.move_legend(
		ax,
		"center left",
		bbox_to_anchor=(1.025, 0.5),
		handles=handles,
		labels=dataset_labels,
		title_fontsize=common.fontsize_small,
		fontsize=common.fontsize_small,
		title='Legend',
	)

	common.save_to_file(fig, os.path.basename(__file__) + "_absolute_" + str(id))

#
# Plot details through all dataset sizes for one route request
#

# The longest distance of the smallest dataset -> longest distance that exists in all datasets
# (assuming all have the same waypoints, which is usually the case)
min_dataset_size=min(dataset_raw["obstacle_vertices_input"]);
beeline_distance_to_plot=max(dataset_raw[dataset_raw["obstacle_vertices_input"] == min_dataset_size]["distance_beeline"])

dataset_filtered=dataset_raw[dataset_raw["distance_beeline"] == beeline_distance_to_plot];
dataset_relevant=dataset_filtered[["obstacle_vertices_input"] + dataset_cols]
dataset=dataset_relevant.melt('obstacle_vertices_input', var_name='aspect', value_name='time')

common.init_seaborn(width=440)

fig, ax = plt.subplots()

common.create_lineplot(
	dataset,
	ycol='time',
	ylabel='Time in ms',
	hue="aspect",
	#yscale='log',
	#errorbar=("pi", 50),
	ax=ax
)

handles, labels = ax.get_legend_handles_labels()
handles=[h for h in handles if not isinstance(h, container.ErrorbarContainer)]

sns.move_legend(
	ax,
	"center left",
	bbox_to_anchor=(1.025, 0.5),
	handles=handles,
	labels=dataset_labels,
	title_fontsize=common.fontsize_small,
	fontsize=common.fontsize_small,
	title='Legend',
)

common.save_to_file(fig, os.path.basename(__file__) + "_absolute_all")

#
# Plot relative numbers
#

for id in dataset_ids:
	fig, ax = plt.subplots()

	#dataset_raw.reset_index(drop=True, inplace=True)
	#dataset_relevant.reset_index(drop=True, inplace=True)
	dataset_filtered=dataset_raw[dataset_raw["obstacle_vertices_input"] == id];
	dataset_relevant=dataset_filtered[["distance_beeline"] + dataset_cols]
	dataset_relevant[dataset_cols]=dataset_filtered[dataset_cols].div(dataset_filtered["avg_time"], axis=0)
	dataset=dataset_relevant.melt('distance_beeline', var_name='aspect', value_name='time')

	common.create_lineplot(
		dataset,
		xcol="distance_beeline",
		xlabel="Beeline distance in km",
		ycol='time',
		ylabel='Share of total time',
		hue="aspect",
		#yscale='log',
		ax=ax
	)

	handles, labels = ax.get_legend_handles_labels()
	handles=[h for h in handles if not isinstance(h, container.ErrorbarContainer)]

	sns.move_legend(
		ax,
		"center left",
		bbox_to_anchor=(1.025, 0.5),
		handles=handles,
		labels=dataset_labels,
		title_fontsize=common.fontsize_small,
		fontsize=common.fontsize_small,
		title='Legend'
	)

	common.save_to_file(fig, os.path.basename(__file__) + "_relative_" + format(id, '05d'))

#
# Plot relative share details through all dataset sizes for one route request
#

# The longest distance of the smallest dataset -> longest distance that exists in all datasets
# (assuming all have the same waypoints, which is usually the case)
min_dataset_size=min(dataset_raw["obstacle_vertices_input"]);
beeline_distance_to_plot=max(dataset_raw[dataset_raw["obstacle_vertices_input"] == min_dataset_size]["distance_beeline"])

dataset_filtered=dataset_raw[dataset_raw["distance_beeline"] == beeline_distance_to_plot];
dataset_relevant=dataset_filtered[["obstacle_vertices_input"] + dataset_cols]
dataset_relevant[dataset_cols]=dataset_filtered[dataset_cols].div(dataset_filtered["avg_time"], axis=0)
dataset=dataset_relevant.melt('obstacle_vertices_input', var_name='aspect', value_name='time')

common.init_seaborn(width=440)

fig, ax = plt.subplots()

common.create_lineplot(
	dataset,
	ycol='time',
	ylabel='Time in ms',
	hue="aspect",
	#yscale='log',
	#errorbar=("pi", 50),
	ax=ax
)

handles, labels = ax.get_legend_handles_labels()
handles=[h for h in handles if not isinstance(h, container.ErrorbarContainer)]

sns.move_legend(
	ax,
	"center left",
	bbox_to_anchor=(1.025, 0.5),
	handles=handles,
	labels=dataset_labels,
	title_fontsize=common.fontsize_small,
	fontsize=common.fontsize_small,
	title='Legend',
)

common.save_to_file(fig, os.path.basename(__file__) + "_relative_all")
