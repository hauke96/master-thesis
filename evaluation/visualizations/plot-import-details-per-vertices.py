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
import matplotlib.ticker as ticker
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

dataset_raw=common.load_dataset(dataset_filter)

#
# Plot absolute numbers
#
def absolute_plot(ax, with_legend=True):
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
		ax=ax
	)

	if with_legend:
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
	else:
		ax.legend([],[], frameon=False)

fig, ax = plt.subplots()
absolute_plot(ax);
common.save_to_file(fig, os.path.basename(__file__) + "_absolute")

#
# Plot relative numbers
#
def relative_plot(ax, with_legend=True, yscale=None):
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
		ax=ax,
		yscale=yscale
	)

	if with_legend:
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
	else:
		ax.legend([],[], frameon=False)

fig, ax = plt.subplots()
relative_plot(ax);
common.save_to_file(fig, os.path.basename(__file__) + "_relative")

#
# Plot both in same figure
#

fig, ax = plt.subplots(ncols=2)
absolute_plot(ax[0], False);
relative_plot(ax[1], yscale="log");

for i in range(len(ax)):
	ax[i].xaxis.set_major_locator(ticker.MultipleLocator(10000))
	labels = ['{:,.0f}'.format(label) + 'k' for label in ax[i].get_xticks()/1000]
	ax[i].set_xticklabels(labels);

common.save_to_file(fig, os.path.basename(__file__) + "_absolute-relative")
