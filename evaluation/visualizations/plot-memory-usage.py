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

def yline(x, label, xoffset=0):
	ax.axvline(
		x=x,
		ymin=0.1,
		linewidth=1
	)
	plt.text(
		x-0.3+xoffset,
		12,
		label,
		fontsize=common.fontsize_small
	)

common.check_args(1)

dataset_name=sys.argv[1]

dataset=common.load_dataset("../results/osm-based-city-ram/"+dataset_name+"/memory.csv")
timestamps=common.load_dataset("../results/osm-based-city-ram/"+dataset_name+"/timestamps.csv")

# Determine start time
timestamp_start=timestamps[timestamps["name"] == "before_graph_generation"]["time"].iloc[0]

# Normalize and convert ms -> s
timestamps["time"]=timestamps["time"] - timestamp_start
timestamps["time"]=timestamps["time"] / 1000
timestamp_get_obstacles_start=timestamps[timestamps["name"] == "graph_creation_get_obstacle_start"]["time"].iloc[0]
timestamp_knn_start=timestamps[timestamps["name"] == "graph_creation_knn_start"]["time"].iloc[0]
timestamp_create_graph_start=timestamps[timestamps["name"] == "graph_creation_create_graph_start"]["time"].iloc[0]
timestamp_merge_start=timestamps[timestamps["name"] == "graph_creation_merge_start"]["time"].iloc[0]
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
	format="large",
	#palette="custom_blue-red"
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
	ax=ax
)

yline(timestamp_knn_start, '1')
yline(timestamp_create_graph_start, '2', -0.3)
yline(timestamp_merge_start, '3', 0.3)
yline(timestamp_after_graph_generation, '4')
yline(timestamp_after_agent, '5')

common.save_to_file(fig, os.path.basename(__file__) + "_osm-based-city-" + dataset_name)
