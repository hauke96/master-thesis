#!/bin/python

'''
Plots detailed import times of the given dataset.

Parameters: {file-filter}

Example: ./plot-import-details-per-vertices.py "../results/pattern-based-rectangles/pattern_*x*_performance_GenerateGraph.csv"
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
	# Only when yscale='log':
	'iteration_time',
	'knn_search_time',
	'build_graph_time',
	'get_obstacle_time',
	'merge_road_graph_time',
	'add_poi_attributes_time',
]
dataset_labels=[
	# Only when yscale='log':
	'Total time',
	'kNN search',
	'Create graph',
	'Get \& prepare\nobstacles',
	'Merge road\nedges',
	'Add POI\nattributes',
]

common.init_seaborn(format="large")

#
# Plot absolute numbers
#

title="HybridVisibilityGraph generation - Durations broken down"
fig_abs, ax_abs = plt.subplots()

dataset_raw=common.load_dataset(dataset_filter)
dataset_relevant=dataset_raw[dataset_cols + ["obstacle_vertices_input"]]
dataset=dataset_relevant.melt('obstacle_vertices_input', var_name='aspect', value_name='time')
dataset["time"] = dataset["time"] / 1000

common.create_lineplot(
	dataset,
	#title,
	ycol='time',
	ylabel='Time in s',
	hue="aspect",
	yscale='log',
	ax=ax_abs
)

handles, labels = ax_abs.get_legend_handles_labels()
handles=[h for h in handles if not isinstance(h, container.ErrorbarContainer)]

sns.move_legend(
	ax_abs,
	"center left",
	bbox_to_anchor=(1.025, 0.5),
	handles=handles,
	labels=dataset_labels,
	title_fontsize=common.fontsize_small,
	fontsize=common.fontsize_small,
	title='Legend'
)

common.save_to_file(fig_abs, os.path.basename(__file__) + "_absolute")

#
# Plot relative numbers
#

title="HybridVisibilityGraph generation - Durations relative share"
fig_rel, ax_rel = plt.subplots()

#dataset_raw.reset_index(drop=True, inplace=True)
#dataset_relevant.reset_index(drop=True, inplace=True)
dataset_relevant=dataset_raw[dataset_cols + ["obstacle_vertices_input"]]
dataset_relevant[dataset_cols]=dataset_raw[dataset_cols].div(dataset_raw["iteration_time"], axis=0)
dataset=dataset_relevant.melt('obstacle_vertices_input', var_name='aspect', value_name='time')
#dataset["time"] = dataset["time"] / 1000

filtered=pd.concat([dataset[dataset["aspect"]=="merge_road_graph_time"], dataset[dataset["aspect"]=="knn_search_time"]])
grouped=dataset.groupby(by=["obstacle_vertices_input", "aspect"]).sum().reset_index()
grouped["avg"] = grouped["time"].div(5)
print(grouped.to_string())

common.create_lineplot(
	dataset,
	#title,
	ycol='time',
	ylabel='Share of total time',
	hue="aspect",
	ax=ax_rel,
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
