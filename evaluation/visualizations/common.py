#!/bin/python

import os
import sys
import glob
import seaborn as sns
import pandas as pd

def check_args(expected_number):
	if len(sys.argv) - 1 != expected_number:
		print("ERROR: Wrong number of arguments: Expected %s, found %s" % expected_number, len(sys.argv))
		print("Expexted parameters: {glob-pattern} {title}")
		sys.exit(1)

def load_dataset(dataset_filter, title):
	dataset_files = glob.glob(dataset_filter)
	dataset_container = []
	for file in dataset_files:
	    df = pd.read_csv(file)
	    dataset_container.append(df)
	
	dataset = pd.concat(dataset_container)
	return dataset

def init_seaborn():
	sns.set(rc={
		"figure.figsize": (5, 4),
		"figure.dpi": 100,
	})
	
	sns.set_style("whitegrid", {
		"font.sans-serif": ["Droid Sans"]
	})

#plot = sns.boxplot(
#	data=dataset,
#	x="total_vertices",
#	y="iteration_time",
#	whis=10,
#)
#figure = plot.get_figure()

def create_lineplot(
		dataset, title = "",
		xcol = "total_vertices",
		ycol = "iteration_time",
		xlabel = "Input vertices",
		ylabel = "Time in ms"
	):
	plot = sns.lineplot(
		data=dataset,
		x=xcol,
		y=ycol,
		marker='o',
		markersize=5,
		err_style="bars",
		errorbar=lambda x: (x.min(), x.max()),
		linewidth=1,
		err_kws={"elinewidth": 1},
		zorder=10,
		clip_on=False,
	)
	plot.set_xlim(0, None)
	plot.set_ylim(0, None)
	plot.set_title(title, pad=15, fontsize=14)
	plot.set_xlabel(xlabel, labelpad=10, fontsize=9)
	plot.set_ylabel(ylabel, labelpad=10, fontsize=9)
	plot.tick_params(axis="both", which="major", labelsize=9)
	#plot.set(yscale="log")
	figure = plot.get_figure()
	return figure

def save_to_file(figure, filename, extension = "png"):
	figure.tight_layout()
	figure.savefig(
		"./"+filename+"."+extension,
		#pad_inches=0,
		#bbox_inches='tight',
	)
