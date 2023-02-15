#!/bin/python

import common
import os
import sys

common.check_args(2)

dataset_filter=sys.argv[1]
title=sys.argv[2]
dataset=common.load_dataset(dataset_filter, title)
dataset["distance"]=dataset["distance"] / 1000

# Convert bytes to MiB
mem_col="max_mem"
dataset[mem_col]=dataset[mem_col] / 1024 / 1024

common.init_seaborn(
	width=6,
	height=4,
	dpi=120,
	palette="custom_blue-red"
)

plot=common.create_lineplot(
	dataset,
	title,
	xcol="distance",
	xlabel="Distance in km",
	ycol=mem_col,
	ylabel="Average memory usage in MiB",
	hue="total_vertices",
	style="total_vertices",
)
common.set_legend(plot, "Amount vertices", dataset["total_vertices"])

common.save_to_file(plot.get_figure(), os.path.basename(__file__))
