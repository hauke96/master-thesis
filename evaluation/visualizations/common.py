#!/bin/python

import os
import sys
import glob
import seaborn as sns
import pandas as pd
import matplotlib.lines as mlines
from matplotlib.colors import ListedColormap
import matplotlib.cm as cm
import textwrap

color_blue="#2779b4"
color_red="#b42727"
color_green="#27b43e"

color_palette_flare=sns.color_palette("flare", as_cmap=True)
color_palette_blue=sns.color_palette("blend:"+color_blue+","+color_blue, as_cmap=True)
color_palette_blue_green=sns.color_palette("blend:"+color_blue+","+color_green, as_cmap=True)
color_palette_blue_red=sns.color_palette("blend:"+color_blue+","+color_blue, as_cmap=True)
color_palette_selected_name="custom_blue"

cm.register_cmap("custom_flare", color_palette_flare)
cm.register_cmap("custom_blue", color_palette_blue)
cm.register_cmap("custom_blue-green", color_palette_blue_green)
cm.register_cmap("custom_blue-red", color_palette_blue_red)

fontsize_small=9
fontsize_large=14

errorbars_minmax=lambda x: (x.nanmin(), x.nanmax())

def check_args(expected_number):
	if len(sys.argv) - 1 != expected_number:
		print("ERROR: Wrong number of arguments: Expected %s, found %s" % (expected_number, len(sys.argv)))
		print("  > %s" % sys.argv[1:])
		print("Expexted parameters: {glob-pattern} {title}")
		sys.exit(1)

def load_dataset(dataset_filter):
	dataset_files=glob.glob(dataset_filter)
	dataset_container=[]
	for file in dataset_files:
		df=pd.read_csv(file)
		dataset_container.append(df)
	
	dataset=pd.concat(dataset_container)
	return dataset

'''
Converts the given pt sizes into inch, which can be used by seaborn/matplotlib.
'''
def get_fig_sizes(width_in_pt, height_in_pt=None, fraction=1):
	inches_per_pt = 1 / 72.27

	fig_width_in = width_in_pt * inches_per_pt

	if height_in_pt == None:
		ratio = 0.575
		fig_height_in = fig_width_in * ratio
	else:
		fig_height_in = height_in_pt * inches_per_pt

	fig_dim = (fig_width_in, fig_height_in)
	return fig_dim


def init_seaborn(
		format="small",
		width=None,
		height=None,
		dpi=120,
		palette='colorblind',
	):

	if width == None:
		if format == "large":
			width=440
			height=174
		elif format == "large_slim":
			width=440
			height=124
		elif format == "small":
			width=220
			height=135

	global color_palette_selected_name

	sns.set(rc={
		"figure.figsize": get_fig_sizes(width, height),
		"figure.dpi": dpi,
	})
	
	sns.set_style("whitegrid", {
		"font.sans-serif": ["Droid Sans"]
	})

	sns.set_palette(palette)
	color_palette_selected_name=palette

#plot=sns.boxplot(
#	data=dataset,
#	x="obstacle_vertices_input",
#	y="iteration_time",
#	whis=10,
#)
#figure=plot.get_figure()

def create_lineplot(
		dataset,
		title="",
		xcol="obstacle_vertices_input",
		xlabel="Input obstacle vertices",
		ycol="iteration_time",
		ylabel="Time in ms",
		hue=None,
		style=None,
		err_style="band",
		errorbar=('pi', 90),
		yscale=None,
		ax=None,
		color=None,
		marker="o",
		scientific_labels=True,
		markersize=5,
	):

	err_kws={"elinewidth": 1} if err_style == "bars" else None

	plot=sns.lineplot(
		data=dataset,
		x=xcol,
		y=ycol,
		hue=hue,
		style=style,
		color=color,
		palette=color_palette_selected_name,
		marker=marker,
		markersize=markersize,
		err_style=err_style,
		errorbar=errorbar,
		err_kws=err_kws,
		linewidth=1,
		zorder=10,
		clip_on=False,
		ax=ax
	)

	if yscale != None:
		plot.set(yscale=yscale)
	else:
		plot.set_xlim(0, None)
		plot.set_ylim(0, None)

	plot.set_title(title, pad=12, fontsize=fontsize_large)
	plot.set_xlabel(xlabel, labelpad=5, fontsize=fontsize_small)
	plot.set_ylabel(ylabel, labelpad=5, fontsize=fontsize_small)
	plot.tick_params(axis="both", which="major", labelsize=fontsize_small)

	if not scientific_labels:
		plot.ticklabel_format(style='plain', axis='x')
		plot.ticklabel_format(style='plain', axis='y')

	return plot

def create_scatterplot(
		dataset,
		title="",
		xcol=None,
		ycol=None,
		xlabel=None,
		ylabel=None,
		hue=None,
		style=None,
		yscale=None,
		ax=None,
		color=None,
		marker="x",
	):

	plot=sns.scatterplot(
		data=dataset,
		x=xcol,
		y=ycol,
		hue=hue,
		style=style,
		palette=color_palette_selected_name,
		marker=marker,
		edgecolor=None,
		color=color,
		linewidth=1,
		s=10,
		legend = False,
		zorder=10,
		clip_on=False,
		ax=ax
	)

	if yscale == "log":
		plot.set(yscale="log")
	else:
		plot.set_xlim(0, None)
		plot.set_ylim(0, None)

	plot.set_title(title, pad=12, fontsize=fontsize_large)
	plot.set_xlabel(xlabel, labelpad=5, fontsize=fontsize_small)
	plot.set_ylabel(ylabel, labelpad=5, fontsize=fontsize_small)
	plot.tick_params(axis="both", which="major", labelsize=fontsize_small)

	return plot

def create_scatter_lineplot(
		dataset,
		title="",
		xcol=None,
		ycol=None,
		xlabel=None,
		ylabel=None,
		ax=None,
		color_scatter=color_blue,
		color_line=color_red,
		marker=None,
	):
	create_scatterplot(
		dataset,
		title,
		ax=ax,
		xcol=xcol,
		xlabel=xlabel,
		ycol=ycol,
		ylabel=ylabel,
		color=color_scatter,
	)
	create_lineplot(
		dataset,
		title,
		ax=ax,
		xcol=xcol,
		xlabel=xlabel,
		ycol=ycol,
		ylabel=ylabel,
		color=color_line,
		marker=marker,
	)

def create_barplot(
		dataset,
		title="",
		xcol="obstacle_vertices_input",
		xlabel="Input obstacle vertices",
		ycol="iteration_time",
		ylabel="Time in ms",
		hue=None,
		capsize=0.075,
		errorbar=None,#('pi', 90),
		errwidth=0.75,
		yscale='linear',
		ax=None,
		color=None,
		scientific_labels=True
	):

	plot=sns.barplot(
		data=dataset,
		x=xcol,
		y=ycol,
		hue=hue,
		color=color,
		palette=color_palette_selected_name,
		capsize=capsize,
		errorbar=errorbar,
		errwidth=errwidth,
		linewidth=1,
		ax=ax
	)

	if yscale != None:
		plot.set(yscale=yscale)
	else:
		plot.set_xlim(0, None)
		plot.set_ylim(0, None)

	plot.set_title(title, pad=12, fontsize=fontsize_large)
	plot.set_xlabel(xlabel, labelpad=5, fontsize=fontsize_small)
	plot.set_ylabel(ylabel, labelpad=5, fontsize=fontsize_small)
	plot.tick_params(axis="both", which="major", labelsize=fontsize_small)

	if not scientific_labels:
		plot.ticklabel_format(style='plain', axis='x')
		plot.ticklabel_format(style='plain', axis='y')

	return plot

def wrap_labels(labels, wrap_width=10):
	return [textwrap.fill(label, wrap_width) for label in labels]

'''
This function selects the legend labels for the given column names
"col_values" and colors them according to color_palette_selected_name.
'''
def set_numeric_legend(plot, title, col_values):
	col_values=col_values.unique()
	col_values.sort()
	col_values=[str(v) for v in col_values]

	handles, labels=plot.get_legend_handles_labels()

	# Generate as many color values as we need according to currently selected
	# colormap
	colors=sns.color_palette(palette=color_palette_selected_name, n_colors=len(col_values))

	# Find out at which index the style items for each col_value start
	col_value_start_index=len(labels) - 1 - labels[::-1].index(col_values[0])

	# Select only the style items for our col_values
	labels=labels[col_value_start_index:]
	handles=handles[col_value_start_index:]

	# Set color to each handle
	for i in range(len(handles)):
		handles[i].set_color(colors[i])

	plot.legend(
		handles=handles,
		labels=labels,
		title=title,
		title_fontsize=fontsize_small,
		fontsize=fontsize_small,
		loc="center left",
		bbox_to_anchor=(1.025, 0.5),
		#borderaxespad=0,
	)

def save_to_file(
		figure,
		filename,
		extension=None,
		no_margin=False,
		h_pad=1.08,
		w_pad=1.08
	):

	if extension == None:
		save_to_file(figure, filename, "png", no_margin, h_pad=h_pad, w_pad=w_pad)
		save_to_file(figure, filename, "pgf", True, h_pad=h_pad, w_pad=w_pad)
		#save_to_file(figure, filename, "pdf", True)
	else:
		figure.tight_layout(pad=0, h_pad=h_pad, w_pad=w_pad)
		figure.savefig(
			"./"+filename+"."+extension,
			pad_inches=0 if no_margin else None,
			bbox_inches='tight' if no_margin else None,
		)
