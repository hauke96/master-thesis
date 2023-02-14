#!/bin/python

import common
import os
import sys

common.check_args(2)

dataset_filter = sys.argv[1]
title = sys.argv[2]
dataset = common.load_dataset(dataset_filter, title)

common.init_seaborn()

figure = common.create_lineplot(
	dataset,
	title,
)

common.save_to_file(figure, os.path.basename(__file__))
