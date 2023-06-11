# The code

This folder contains the C# code of my thesis. I wrote it completely on a Linux machine using the Rider IDE and it's based on .NET 6.0.

All descriptions below are focused on Rider but other IDEs should work as well.

## Projects

Open the solution. There should be at least the following three projects:

1. `GeoJsonRouting` takes one agent and calculates one route through a given GeoJSON dataset. See below for further details.
2. `Wavefront` contains the main routing algorithm.
3. `Wavefront.Test` contains unit tests for the `Wavefront` project.

## The `GeoJsonRouting` project

This projects expects an `obstacle.geojson` file in the `Resources` folder. This GeoJSON file must have one node tagged with `start=...` and one different node with `destination=...` (replace `...` with anything, the exact value here is irrelevant). The agent created in this project will then travel from the start to the destination node.

### Run it

Starting this project works by just hitting the play/run button.

### Example datasets

Take a look at the [`Resources`](./GeoJsonRouting/Resources) folder, it contains several example datasets including real world OpenStreetMap dumps.
