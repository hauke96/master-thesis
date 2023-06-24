#!/bin/python

'''
Plots the ratio between real and beeline distance. The real route distance is always longer than the beeline distance by a factor of f>=1.

Parameters: {file-filter}

Example: ./plot-routing-length-factor.py "../results/pattern-based-rectangles/pattern_*x*_performance_Routing.csv"
'''

import common
import os
import sys
import seaborn as sns
import matplotlib.pyplot as plt

common.check_args(1)

dataset_filter=sys.argv[1]
title="Routing - Ratio between beeline and route distance"
dataset=common.load_dataset(dataset_filter)
dataset["distance_route"]=dataset["distance_route"] / 1000
dataset["distance_beeline"]=dataset["distance_beeline"] / 1000
dataset["distance_factor"]=dataset["distance_route"] / dataset["distance_beeline"]

q=dataset["distance_factor"].quantile(0.98)
dataset.loc[dataset["distance_factor"] <= q, "outlier"]=False
dataset.loc[dataset["distance_factor"] > q, "outlier"]=True

# Exclude outliers from dataset:
dataset["distance_factor"]=dataset["distance_factor"].where(dataset["distance_factor"] < q)
dataset.reset_index(drop=True, inplace=True)

common.init_seaborn(
	width=6,
	height=4,
	dpi=120,
)

fig, ax=plt.subplots()

common.create_scatter_lineplot(
	dataset,
	#title,
	ax=ax,
	xcol="obstacle_vertices_input",
	xlabel="Input obstacle vertices",
	ycol="distance_factor",
	ylabel="Beeline distance / route distance",
)
ax.set_ylim(1, None)

common.save_to_file(fig, os.path.basename(__file__))
