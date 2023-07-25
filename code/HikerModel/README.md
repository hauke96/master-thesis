This model creates one agent walking each point of a given linestring by creating routes using the hybrid visibility routing.

## Setup

The default setup already contains all necessary files but own files can be added quite easily:

1. Add a `Resources/obstacle.geojson` file containing obstacles *and* roads
2. Add a `Resources/waypoints.geojson` file with one linestring. The agent will visit each coordinate of this linestring.

## Run it

Simply hit the "play" button of your IDE.
