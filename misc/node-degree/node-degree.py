#!/usr/bin/env python

import sys

node_counts = dict()

with open(sys.argv[1], "r") as file:
	for line in file:
		line = line.strip()
		segments = line.split(" ")
		for i in range(len(segments)):
			# remove all tags (saves space)
			if segments[i][0] == "T":
				segments[i] = "T"

			# For ways: 
			if segments[0][0] == "w" and segments[i][0] == "N":
				# 1. Ignore first char (as it's "N" denoting the node list"
				# 2. Split by the separating ","
				node_ids = segments[i][1:].split(",")

				# Start node
				if node_ids[0] in node_counts:
					node_counts[node_ids[0]] += 1
				else:
					node_counts[node_ids[0]] = 1

				# End node
				if node_ids[-1] in node_counts:
					node_counts[node_ids[-1]] += 1
				else:
					node_counts[node_ids[-1]] = 1

				# Intermediate nodes
				for node_id in node_ids[1:-1]:
					if node_id in node_counts:
						node_counts[node_id] += 2
					else:
						node_counts[node_id] = 2

min_degree=1

# Create OPL output with all nodes taken into account
if len(sys.argv) > 2 and sys.argv[2] == "opl":
	with open(sys.argv[1], "r") as file:
		for line in file:
			line = line.strip()
			segments = line.split(" ")
			if segments[0][0] == "n" and segments[0] in node_counts and node_counts[segments[0]]>=min_degree:
				for i in range(len(segments)):
					# remove all tags (saves space)
					if segments[i][0] == "T":
						segments[i] = "Tcount="+str(node_counts[segments[0]])
				print(" ".join(segments))
	print()
	print()

node_counts=dict((k,v) for k,v in node_counts.items() if v>=min_degree)
degree = sum(node_counts.values()) / len(node_counts)

print("Node degree histogram (degree with amount of nodes):")
for d in range(max(node_counts.values())):
	s = sum(1 for k in node_counts.values() if k == d)
	print("  "+str(d)+"="+str(s))

print("Average node degree:")
print("  "+str(degree))

