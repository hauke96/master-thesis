/*
Copyright (C) 2005-2018 NetTopologySuite team

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES,
INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

License information: https://github.com/NetTopologySuite/NetTopologySuite/blob/develop/License.md

This is a copy of the internal GeometrySnapper.SnapTransformer class from the NetTopologySuite. The original class
is used via the GeometrySnapper.SnapTo method. However, this method calls the very inefficient
GeometrySnapper.ExtractDestinationCoordinates method, which causes a huge performance overhead. 
*/

using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.Operation.Overlay.Snap;

namespace DatasetCreator;

public class SnapTransformer : GeometryTransformer
{
    private readonly double _snapTolerance;
    private readonly Coordinate[] _snapPts;

    public SnapTransformer(double snapTolerance, Coordinate[] snapPts)
    {
        _snapTolerance = snapTolerance;
        _snapPts = snapPts;
    }

    protected override CoordinateSequence TransformCoordinates(CoordinateSequence coords, Geometry parent)
    {
        var srcPts = coords.ToCoordinateArray();
        var newPts = SnapLine(srcPts, _snapPts);
        return Factory.CoordinateSequenceFactory.Create(newPts);
    }

    private Coordinate[] SnapLine(Coordinate[] srcPts, Coordinate[] snapPts)
    {
        var snapper = new LineStringSnapper(srcPts, _snapTolerance);
        return snapper.SnapTo(snapPts);
    }
}