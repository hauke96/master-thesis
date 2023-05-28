#!/bin/python

'''
Plots for each step of the routing the time per meter (y) along the number of input vertices (x).

Parameters: {file-filter}

Example: ./plot-routing-time-per-length.py "../results/pattern-based-rectangles/pattern_*x*_performance_Routing.csv"
'''

import common
import os
import sys
import matplotlib.pyplot as plt

common.check_args(1)

dataset_filter=sys.argv[1]
dataset=common.load_dataset(dataset_filter)

common.init_seaborn(width=6, height=4, dpi=120)

#
# Relative to avg_time
#

title="Routing - Durations per distance"
avg_time_per_distance=dataset["avg_time"] / dataset["distance_beeline"]
dataset["avg_time_per_distance"]=avg_time_per_distance

fig, ax=plt.subplots()

common.create_scatterplot(
	dataset,
	title,
	ax=ax,
	xcol="total_vertices",
	xlabel="Input vertices",
	ycol="avg_time_per_distance",
	ylabel="Time per meter in ms",
	color="#2779b4",
	#yscale="log",
)
common.create_lineplot(
	dataset,
	title,
	ax=ax,
	ycol="avg_time_per_distance",
	ylabel="Time per meter in ms",
	color="#b42727",
	#yscale="log",
)

common.save_to_file(fig, os.path.basename(__file__) + "_avg-time", "png")

#
# Relative to astar_avg_time
#

title="Routing - Duration of A* per distance"
astar_avg_time=dataset["astar_avg_time"] / dataset["distance_beeline"]
dataset["astar_avg_time"]=astar_avg_time

fig, ax=plt.subplots()

common.create_scatterplot(
	dataset,
	title,
	ax=ax,
	xcol="total_vertices",
	xlabel="Input vertices",
	ycol="astar_avg_time",
	ylabel="A* time per meter in ms",
	color="#2779b4",
	#yscale="log",
)
common.create_lineplot(
	dataset,
	title,
	ax=ax,
	ycol="astar_avg_time",
	ylabel="A* time per meter in ms",
	color="#b42727",
	#yscale="log",
)

common.save_to_file(fig, os.path.basename(__file__) + "_astar-time", "png")

#
# Relative to add_positions_to_graph_avg_time
#

title="Routing - Duration of connecting positions per distance"
add_positions_to_graph_avg_time=dataset["add_positions_to_graph_avg_time"] / dataset["distance_beeline"]
dataset["add_positions_to_graph_avg_time"]=add_positions_to_graph_avg_time

fig, ax=plt.subplots()

common.create_scatterplot(
	dataset,
	title,
	ax=ax,
	xcol="total_vertices",
	xlabel="Input vertices",
	ycol="add_positions_to_graph_avg_time",
	ylabel="Time per meter in ms",
	color="#2779b4",
	#yscale="log",
)
common.create_lineplot(
	dataset,
	title,
	ax=ax,
	ycol="add_positions_to_graph_avg_time",
	ylabel="Time per meter in ms",
	color="#b42727",
	#yscale="log",
)

common.save_to_file(fig, os.path.basename(__file__) + "_add-pos-time", "png")
