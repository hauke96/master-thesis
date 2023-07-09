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

common.check_args(0)

dataset_filters=[
	"../results/pattern-based-rectangles/*_GenerateGraph.csv",
	"../results/pattern-based-maze/*_GenerateGraph.csv",
	"../results/pattern-based-circles/*_GenerateGraph.csv"
]

common.init_seaborn(format='large_slim')

#
# Plot absolute numbers
#

fig, ax = plt.subplots(ncols=3)

for i in range(len(dataset_filters)):
	dataset=common.load_dataset(dataset_filters[i])
	dataset["iteration_time_s"]=dataset["iteration_time"]/1000

	plot=common.create_lineplot(
		dataset,
		ycol="iteration_time_s",
		ylabel=None if i>0 else "Time in s",
		ax=ax[i]
	)

	if i==0 or i==1:
		plot.set_ylim(0, 235)
		plot.set_xlim(0, 33000)

	ax[i].legend([],[], frameon=False)
	ax[i].xaxis.set_major_locator(ticker.MultipleLocator(10000))
	labels = ['{:,.0f}'.format(label) + 'k' for label in ax[i].get_xticks()/1000]
	ax[i].set_xticklabels(labels);

common.save_to_file(fig, os.path.basename(__file__) + "_absolute")
