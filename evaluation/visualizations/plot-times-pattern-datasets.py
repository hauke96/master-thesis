#!/bin/python

'''
Plots the import times ("iteration_time" column) of the pattern based datasets.

Parameters: none
'''

import common
import os
import sys
import seaborn as sns
import matplotlib.pyplot as plt
import matplotlib.ticker as ticker
import pandas as pd
from matplotlib import container

common.check_args(0)

titles=[
	"Rectangles",
	"Maze",
	"Circles"
]

common.init_seaborn(format='large')

#
# Plot import times
#
dataset_filters_import=[
	"../results/pattern-based-rectangles/*_GenerateGraph.csv",
	"../results/pattern-based-maze/*_GenerateGraph.csv",
	"../results/pattern-based-circles/*_GenerateGraph.csv"
]

fig, ax = plt.subplots(ncols=3)

for i in range(len(dataset_filters_import)):
	dataset=common.load_dataset(dataset_filters_import[i])
	dataset["iteration_time_s"]=dataset["iteration_time"]/1000

	plot=common.create_lineplot(
		dataset,
		ycol="iteration_time_s",
		ylabel=None if i>0 else "Time in s",
		ax=ax[i]
	)
	plot.set_title(titles[i], pad=8, fontsize=common.fontsize_small)

	if i==0 or i==1:
		plot.set_ylim(0, 235)
		plot.set_xlim(0, 33000)

	ax[i].legend([],[], frameon=False)
	ax[i].xaxis.set_major_locator(ticker.MultipleLocator(10000))
	labels = ['{:,.0f}'.format(label) + 'k' for label in ax[i].get_xticks()/1000]
	ax[i].set_xticklabels(labels);

common.save_to_file(fig, os.path.basename(__file__) + "_import")

#
# Plot routing times
#
dataset_filters_routing=[
	"../results/pattern-based-rectangles/*_Routing.csv",
	"../results/pattern-based-maze/*_Routing.csv",
	"../results/pattern-based-circles/*_Routing.csv"
]

dataset_cols=[
	'avg_time',
	'astar_avg_time',
	'add_positions_to_graph_avg_time',
	'restore_avg_time',
]
dataset_labels=[
	"Total time",
	'A* routing',
	'Connect\nsource \\&\ndestination\nvertices',
	'Restoring\ngraph',
]

common.init_seaborn(width=480, height=174)

fig, ax = plt.subplots(ncols=3)

for i in range(len(dataset_filters_routing)):
	dataset_raw=common.load_dataset(dataset_filters_routing[i])
	dataset_raw["distance_beeline"]=dataset_raw["distance_beeline"] / 1000
	#dataset["avg_time_s"]=dataset["avg_time"]/1000

	# The longest distance of the smallest dataset -> longest distance that exists in all datasets
	# (assuming all have the same waypoints, which is usually the case)
	min_dataset_size=min(dataset_raw["obstacle_vertices_input"]);
	beeline_distance_to_plot=max(dataset_raw[dataset_raw["obstacle_vertices_input"] == min_dataset_size]["distance_beeline"])

	dataset_filtered=dataset_raw[dataset_raw["distance_beeline"] == beeline_distance_to_plot];
	dataset_relevant=dataset_filtered[["obstacle_vertices_input"] + dataset_cols]
	dataset=dataset_relevant.melt('obstacle_vertices_input', var_name='aspect', value_name='time')

	plot=common.create_lineplot(
		dataset,
		ycol='time',
		ylabel=None if i>0 else "Time in ms",
		hue="aspect",
		ax=ax[i]
	)
	plot.set_title(titles[i], pad=8, fontsize=common.fontsize_small)

	#if i==0 or i==1:
	#	plot.set_ylim(0, 235)
	#	plot.set_xlim(0, 33000)

	ax[i].xaxis.set_major_locator(ticker.MultipleLocator(10000))
	labels = ['{:,.0f}'.format(label) + 'k' for label in ax[i].get_xticks()/1000]
	ax[i].set_xticklabels(labels);
	ax[i].tick_params(axis='y', pad=-2)

ax[0].legend([],[], frameon=False)
ax[1].legend([],[], frameon=False)
handles, labels = ax[2].get_legend_handles_labels()
handles=[h for h in handles if not isinstance(h, container.ErrorbarContainer)]

sns.move_legend(
	ax[2],
	"center left",
	bbox_to_anchor=(1.025, 0.5),
	handles=handles,
	labels=dataset_labels,
	title_fontsize=common.fontsize_small,
	fontsize=common.fontsize_small,
	title='Legend',
)

common.save_to_file(fig, os.path.basename(__file__) + "_routing", w_pad=0.65)

