#!/bin/python

import common
import os
import sys

common.check_args(2)

dataset_filter=sys.argv[1]
title=sys.argv[2]
dataset=common.load_dataset(dataset_filter, title)

common.init_seaborn(width=6, height=4, dpi=120)

plot=common.create_lineplot(
	dataset,
	title,
)

common.save_to_file(plot.get_figure(), os.path.basename(__file__), "pgf")
