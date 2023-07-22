#!/bin/python

'''
Plots the memory usage for a given recording created with the "record-ram-usage.sh" script.

Parameters: {dataset-name}

Example: ./plot-routing-memory.py "1km2"
'''

import common
import os
import sys
import matplotlib.pyplot as plt
import matplotlib.transforms as mtransforms

#trans = mtrans.Affine2D().translate(-5, 10)

def yline(ax, x, label, fig, xoffset=0):
	trans_offset = mtransforms.offset_copy(
		ax.transData,
		fig=fig,
		x=-0.035+xoffset,
		y=0.05,
		units='inches'
	)
	ax.axvline(
		x=x,
		ymin=0.1,
		linewidth=0.5,
		color=common.color_red
	)
	plt.text(
		x,#-0.3+xoffset,
		0,#12,
		label,
		fontsize=common.fontsize_small,
		transform=trans_offset
	)

def plot(dataset_name):
	dataset=common.load_dataset("../results/osm-based-city-ram/"+dataset_name+"/memory.csv")
	timestamps=common.load_dataset("../results/osm-based-city-ram/"+dataset_name+"/timestamps.csv")

	# Determine start time
	timestamp_start=timestamps[timestamps["name"] == "before_graph_generation"]["time"].iloc[0]

	# Normalize and convert ms -> s
	timestamps["time"]=timestamps["time"] - timestamp_start
	timestamps["time"]=timestamps["time"] / 1000
	timestamp_get_obstacle_neighbors_start=timestamps[timestamps["name"] == "graph_creation_obstacle_neighbors_start"]["time"].iloc[0]
	timestamp_knn_start=timestamps[timestamps["name"] == "graph_creation_knn_start"]["time"].iloc[0]
	timestamp_create_graph_start=timestamps[timestamps["name"] == "graph_creation_create_graph_start"]["time"].iloc[0]
	timestamp_merge_prepare_start=timestamps[timestamps["name"] == "graph_creation_merge_prepare_start"]["time"].iloc[0]
	timestamp_merge_insert_start=timestamps[timestamps["name"] == "graph_creation_merge_insert_start"]["time"].iloc[0]
	timestamp_after_graph_generation=timestamps[timestamps["name"] == "after_graph_generation"]["time"].iloc[0]
	timestamp_after_agent_init=timestamps[timestamps["name"] == "after_agent_init"]["time"].iloc[0]
	timestamp_after_agent=timestamps[timestamps["name"] == "after_agent"]["time"].iloc[0]

	# Normalize and convert ms -> s + filtering for values before actual start of algorithm
	dataset["time"]=dataset["time"] - timestamp_start
	dataset["time"]=dataset["time"] / 1000
	dataset=dataset[dataset["time"] >= 0]

	# Convert kB to MB
	mem_col="mb"
	mem_col_label="RAM usage in MB"
	dataset[mem_col]=dataset["kb"] / 1000

	common.init_seaborn(
		width=440,
		height=143
	)

	fig, ax=plt.subplots()
	plot=common.create_lineplot(
		dataset,
		#title,
		xcol="time",
		xlabel="Seconds after start",
		ycol=mem_col,
		ylabel=mem_col_label,
		marker=None,
		linewidth=0.5,
		ax=ax
	)

	yline(ax, timestamp_get_obstacle_neighbors_start, '1', fig)
	yline(ax, timestamp_knn_start, '2', fig)
	yline(ax, timestamp_create_graph_start, '3', fig, -0.04)
	yline(ax, timestamp_merge_prepare_start, '4', fig, 0.04)
	yline(ax, timestamp_merge_insert_start, '5', fig)
	yline(ax, timestamp_after_graph_generation, '6', fig)
	yline(ax, timestamp_after_agent, '7', fig)

	common.save_to_file(fig, os.path.basename(__file__) + "_osm-based-city-" + dataset_name)

common.check_args(0)

plot("0,5km2")
plot("1km2")
plot("1,5km2")
plot("2km2")
plot("3km2")
plot("4km2")
