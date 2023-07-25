# The code

This folder contains the C# code of my thesis. I wrote it completely on a Linux machine using the Rider IDE and it's based on .NET 7.0.

All descriptions below are focused on Rider but other IDEs should work as well.

## Projects

These are the relevant projects of the hybrid routing algorithm:

* `HybridVisibilityGraphRouting`: The actual algorithm.
* `HybridVisibilityGraphRouting.Tests`: Unit and integration tests.
* `Triangulation`: Contains simple triangulation method, which requires a newer NTS version not used by MARS at the point of development.

Additional projects are:

* `HikerModel`: Model used for testing and performance evaluation. Further documentation can be found there.
* `DatasetCreator`: Project used to create pattern-based datasets for the performance evaluation.
* `GeoJsonRouting`: Example project of an agent walking randomly between markes source and destination locations within a GeoJSON input file.
* `NetworkRoutingPlayground`: Test project to try out graph-based routing.
