#!/bin/python

import os
import sys
import glob
import seaborn as sns
import pandas as pd
import matplotlib.lines as mlines

color_palette_flare=sns.color_palette("flare", as_cmap=True)
color_palette_blue=sns.color_palette("blend:#829cc8,#55784e", as_cmap=True)

fontsize_small=9
fontsize_large=14

def check_args(expected_number):
	if len(sys.argv) - 1 != expected_number:
		print("ERROR: Wrong number of arguments: Expected %s, found %s" % expected_number, len(sys.argv))
		print("Expexted parameters: {glob-pattern} {title}")
		sys.exit(1)

def load_dataset(dataset_filter, title):
	dataset_files=glob.glob(dataset_filter)
	dataset_container=[]
	for file in dataset_files:
	    df=pd.read_csv(file)
	    dataset_container.append(df)
	
	dataset=pd.concat(dataset_container)
	return dataset

def init_seaborn(
		width=5,
		height=4,
		dpi=100,
	):

	sns.set(rc={
		"figure.figsize": (width, height),
		"figure.dpi": dpi,
	})
	
	sns.set_style("whitegrid", {
		"font.sans-serif": ["Droid Sans"]
	})

#plot=sns.boxplot(
#	data=dataset,
#	x="total_vertices",
#	y="iteration_time",
#	whis=10,
#)
#figure=plot.get_figure()

def create_lineplot(
		dataset, title="",
		xcol="total_vertices",
		ycol="iteration_time",
		xlabel="Input vertices",
		ylabel="Time in ms",
		hue=None,
		style=None,
		palette=None,
	):

	plot=sns.lineplot(
		data=dataset,
		x=xcol,
		y=ycol,
		hue=hue,
		style=style,
		palette=palette,
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
	plot.set_title(title, pad=12, fontsize=fontsize_large)
	plot.set_xlabel(xlabel, labelpad=5, fontsize=fontsize_small)
	plot.set_ylabel(ylabel, labelpad=5, fontsize=fontsize_small)
	plot.tick_params(axis="both", which="major", labelsize=fontsize_small)
	#plot.set(yscale="log")
	return plot

'''
This function manually creates colored entries for each unique value in the given list.
'''
def set_legend(plot, title, col_values):
	col_values=col_values.unique()
	col_values.sort()
	col_values = [str(v) for v in col_values]

	colors=sns.color_palette()[:len(col_values)]

	# TODO Fix colors
	handles=[mlines.Line2D([], [], color=color, linestyle='--') for color in colors]
	labels=col_values

	plot.legend(
		title=title,
		title_fontsize=fontsize_small,
		fontsize=fontsize_small,
		handles=handles,
		labels=labels
	)

def save_to_file(
		figure,
		filename,
		extension="png",
		no_margin=False
	):

	figure.tight_layout()
	figure.savefig(
		"./"+filename+"."+extension,
		pad_inches=0 if no_margin else None,
		bbox_inches='tight' if no_margin else None,
	)
