#!/bin/python

import os
import sys
import glob
import seaborn as sns
import pandas as pd
import matplotlib.lines as mlines
from matplotlib.colors import ListedColormap
import matplotlib.cm as cm

color_palette_flare=sns.color_palette("flare", as_cmap=True)
color_palette_blue_green=sns.color_palette("blend:#829cc8,#55784e", as_cmap=True)
color_palette_blue_red=sns.color_palette("blend:#829cc8,#c86a6a", as_cmap=True)
color_palette_selected_name=None

cm.register_cmap("custom_flare", color_palette_flare)
cm.register_cmap("custom_blue-green", color_palette_blue_green)
cm.register_cmap("custom_blue-red", color_palette_blue_red)

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
		palette=None,
	):

	global color_palette_selected_name

	sns.set(rc={
		"figure.figsize": (width, height),
		"figure.dpi": dpi,
	})
	
	sns.set_style("whitegrid", {
		"font.sans-serif": ["Droid Sans"]
	})

	sns.set_palette(palette)
	color_palette_selected_name=palette

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
	):

	plot=sns.lineplot(
		data=dataset,
		x=xcol,
		y=ycol,
		hue=hue,
		style=style,
		palette=color_palette_selected_name,
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
This function selects the legend labels matching the given values and colors
them according to color_palette_selected_name.
'''
def set_legend(plot, title, col_values):
	col_values=col_values.unique()
	col_values.sort()
	col_values=[str(v) for v in col_values]

	handles, labels=plot.get_legend_handles_labels()
	colors=sns.color_palette(palette=color_palette_selected_name, n_colors=len(col_values))
	
	new_handles=[None] * len(col_values)
	new_labels=[None] * len(col_values)

	# Get the label and handle for each col_value, set its color and store in 
	# these arrays
	for i in range(len(col_values)):
		value=col_values[i]
		index=labels.index(value)

		label=labels[index]
		handle=handles[index]

		handle.set_color(colors[i])

		new_labels[i]=label
		new_handles[i]=handle

	plot.legend(
		handles=new_handles,
		labels=new_labels,
		title=title,
		title_fontsize=fontsize_small,
		fontsize=fontsize_small,
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
