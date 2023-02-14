#!/bin/python

import common
import os
import sys

common.check_args(2)

dataset_filter = sys.argv[1]
title = sys.argv[2]
dataset = common.load_dataset(dataset_filter, title)

time_per_distance = dataset["avg_time"] / dataset["distance"]
dataset["time_per_distance"] = time_per_distance

common.init_seaborn()

figure = common.create_lineplot(
	dataset,
	title,
	ycol = "time_per_distance",
	ylabel = "Time per meter"
)

common.save_to_file(figure, os.path.basename(__file__))
