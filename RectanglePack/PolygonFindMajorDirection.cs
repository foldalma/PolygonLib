using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace RectanglePack
{
    public class PolygonFindMajorDirection : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public PolygonFindMajorDirection()
          : base("PolygonFindMajorDirection", "PolygonMajorDir",
              "Find the major axis / direction of closed polyline curve",
              "Polyline", "Utility")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("polygon", "p", "Polygon to find major axis of", GH_ParamAccess.item);
            pManager.AddIntegerParameter("numAngleCategories", "nCat", "Number of possible categories (bins) for angles", GH_ParamAccess.item);
            pManager.AddBooleanParameter("debug", "debug", "Toggle debug mode", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("orientation", "or", "Orientation or major axis of input polygon", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double or = 0.0;
            PolyCurve polygon = new PolyCurve();
            int numCategories = 0;
            bool debug = false;

            DA.GetData(0, ref polygon);
            DA.GetData(1, ref numCategories);
            DA.GetData(2, ref debug);

            if (polygon.IsValid && !polygon.IsShort(Rhino.RhinoMath.ZeroTolerance) && polygon.IsClosed)
            {
                FindMajorEdgeOrientation(polygon, ref or, numCategories, debug);
            }
            else
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input polygon / curve is invalid, too short, or not closed");

            DA.SetData(0, or);
        }

        private void FindMajorEdgeOrientation(Curve polygon, ref double orientation, int numCategories, bool debug)
        {
            List<Curve> segments = polygon.DuplicateSegments().ToList();
            CurveOrientation co = CurveOrientation.Clockwise;
            List<double> lengthTable = CreateTable(numCategories);
            Dictionary<int, int> curveTable = new Dictionary<int, int>();
            Vector3d xyVector = Vector3d.XAxis;
            double angleBinSize = ((2.0 * Math.PI) / numCategories);

            if (numCategories < segments.Count)
                numCategories = segments.Count;

            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i].ClosedCurveOrientation() == co)
                    segments[i].Reverse();

                double currLength = segments[i].GetLength();

                Vector3d segmentVector = Vector3d.Subtract(new Vector3d(segments[i].PointAtEnd), new Vector3d(segments[i].PointAtStart));
                Vector3d cross = Vector3d.CrossProduct(xyVector, segmentVector);
                double currAngle = Vector3d.VectorAngle(segmentVector, xyVector, cross);

                int category = (int)(currAngle / angleBinSize);
                if (!curveTable.ContainsKey(category))
                    curveTable[category] = i;

                if (debug)
                {
                    Rhino.RhinoApp.WriteLine("INDEX " + i.ToString());
                    Rhino.RhinoApp.WriteLine("ANGLE " + Rhino.RhinoMath.ToDegrees(currAngle).ToString());
                    Rhino.RhinoApp.WriteLine("CATEGORY " + category.ToString());
                }
                lengthTable[category] += currLength;
            }

            // Find maximum element in length array
            double max = lengthTable.Max();
            int argmax = Array.IndexOf(lengthTable.ToArray(), max);

            if (debug) {
                Rhino.RhinoApp.WriteLine("max " + max.ToString());
                Rhino.RhinoApp.WriteLine("argmax " + argmax.ToString());
            }
            
            orientation = argmax * ((2.0 * Math.PI) / numCategories);

            // Double-check curve orientation
            Curve maxCurve = segments[curveTable[argmax]];
            Vector3d maxCurveVector = Vector3d.Subtract(new Vector3d(maxCurve.PointAtEnd), new Vector3d(maxCurve.PointAtStart));
            Line checkLine = new Line(maxCurve.PointAtStart, xyVector);
            checkLine.Transform(Transform.Rotation(orientation, maxCurve.PointAtStart));

            double angle = Vector3d.VectorAngle(maxCurveVector, checkLine.Direction);
            if (angle > angleBinSize)
                orientation = Math.PI - orientation;

        }

        private double AngleBetween(Vector3d vector1, Vector3d vector2)
        {
            double sin = vector1.X * vector2.Y - vector2.Y * vector1.Y;
            double cos = vector1.X * vector2.X + vector1.Y * vector2.Y;

            return Math.Atan2(sin, cos) * (180 / Math.PI);

        }


        private List<double> CreateTable(int numCategories)
        {
            List<double> lengthTable = new List<double>();

            for (int i = 0; i < numCategories; i++)
            {
                lengthTable.Add(0);
            }
            return lengthTable;
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("9ef00dac-fa89-4eb8-96ec-af9e9e24fc9a"); }
        }
    }
}