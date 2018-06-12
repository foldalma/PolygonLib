using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;

// In order to load the result of this wizard, you will also need to
// add the output bin/ folder of this project to the list of loaded
// folder in Grasshopper.
// You can use the _GrasshopperDeveloperSettings Rhino command for that.

namespace RectanglePack
{
    public class RectanglePackComponent : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public RectanglePackComponent()
          : base("RectanglePack", "Nickname",
              "Pack boundary curve with rectangles",
              "Category", "Subcategory")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Boundaries", "Bnd", "List of one or more bounaries to pack", GH_ParamAccess.list);
            pManager.AddCurveParameter("Rectangle", "Rect", "Rectangle", GH_ParamAccess.list);
            pManager.AddNumberParameter("Orientation", "Or", "Orientation of rectangles in each boundary", GH_ParamAccess.list);
            pManager.AddNumberParameter("Offset", "O", "Offset", GH_ParamAccess.item);
            pManager.AddNumberParameter("SearchSpacing", "Search", "Spacing while searching for initial fit", GH_ParamAccess.item);
            pManager.AddBooleanParameter("bRotation", "bR", "Should rotate rectangles or not", GH_ParamAccess.item);
            pManager.AddBooleanParameter("bAxisAligned", "bA", "Axis aligned rectangle orientation", GH_ParamAccess.item);

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("PackedRectangles", "Packed", "Rectangles that have been fit", GH_ParamAccess.item);
            pManager.AddPointParameter("GridPoints", "GP", "Base points for grid alignment", GH_ParamAccess.list);
            pManager.AddVectorParameter("RectangleVectors", "RV", "Rectangle move vectors", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {

            List<Curve> boundaries = new List<Curve>();
            Rectangle3d fitRect = new Rectangle3d();
            List<double> orientations = new List<double>();
            bool bRotation = false;
            bool bAxisAligned = false;
            double searchSpacing = 1.0;
            double offset = 5.0;

            DA.GetData(0, ref boundaries);
            DA.GetData(1, ref fitRect);
            DA.GetData(2, ref orientations);
            DA.GetData(3, ref offset);
            DA.GetData(4, ref searchSpacing);
            DA.GetData(5, ref bRotation);
            DA.GetData(6, ref bAxisAligned);

            List<Rectangle3d> foundRects = new List<Rectangle3d>();
            List<Point3d> pts = new List<Point3d>();
            List<Vector3d> vs = new List<Vector3d>();

            foreach (Curve boundary in boundaries)
            {
                if (fitRect.IsValid)
                {
                    BoundingBox bx = boundary.GetBoundingBox(false);
                    foundRects.AddRange(CheckFit(bx, fitRect, boundary, offset, bRotation, pts, searchSpacing, bAxisAligned, vs));
                }
            }

            DA.SetData(0, foundRects);
            DA.SetData(1, pts);
            DA.SetData(2, vs);
        }

        private void RunScript(Curve boundary, double offset, double searchSpacing, List<Rectangle3d> fitRects, bool withRotation, object withPartition, bool axisAligned, ref object packedRects, ref object outPts, ref object moveVs)
        {

            BoundingBox bx = boundary.GetBoundingBox(false);
            List<Rectangle3d> foundRects = new List<Rectangle3d>();
            packedRects = new List<Rectangle3d>();
            List<Point3d> pts = new List<Point3d>();
            List<Vector3d> vs = new List<Vector3d>();

            foreach (Rectangle3d fitRect in fitRects)
            {
                if (fitRect.IsValid)
                {
                    foundRects.AddRange(CheckFit(bx, fitRect, boundary, offset, withRotation, pts, searchSpacing, axisAligned, vs));
                }
            }

            packedRects = foundRects;
            outPts = pts;
            moveVs = vs;
        }

        // <Custom additional code> 
        private List<double> FindSpacing(Rectangle3d rect, double offset)
        {
            List<double> spacing = new List<double>(2);
            double dX = 0;
            double dY = 0;

            Vector3d v1 = rect.Plane.XAxis;
            Vector3d v2 = Plane.WorldXY.XAxis;

            Vector3d translationVector = new Vector3d(rect.Plane.XAxis.X, rect.Plane.YAxis.Y, 1);
            // translationVector.Unitize();


            if (Vector3d.VectorAngle(v1, v2) < 0.5)
            {
                dX = (offset * 2 + rect.Width) * translationVector.X;
                dY = (offset * 2 + rect.Height) * translationVector.Y;
            }
            else
            {
                dX = (offset * 2 + rect.Height) * translationVector.Y;
                dY = (offset * 2 + rect.Width) * translationVector.X;
            }


            /* dX = (offset + rect.Width) * translationVector.X;
             dY = (offset + rect.Height) * translationVector.Y; */

            spacing.Add(dX);
            spacing.Add(dY);

            return spacing;
        }

        private IList<Rectangle3d> CheckFit(BoundingBox bx, Rectangle3d rect, Curve boundary, double offset, bool withRotation, List<Point3d> outPts, double searchSpacing, bool axisAligned, List<Vector3d> vs)
        {
            bool IsRotated = false;
            bool searching = true;

            double angle = 1.5708;

            List<double> spacing = FindSpacing(rect, offset);
            IList<Rectangle3d> tRects = new List<Rectangle3d>();

            double dX = searchSpacing;
            double dY = searchSpacing;
            double xMin = bx.Min.X;
            double yMin = bx.Min.Y;
            Point3d p = new Point3d(xMin, yMin, 0.0);

            Vector3d translationVector = new Vector3d(rect.Plane.XAxis.X, rect.Plane.YAxis.Y, 1);
            translationVector.Unitize();

            for (double y = yMin; y < bx.Max.Y; y += dY)
            {
                for (double x = xMin; x < bx.Max.X; x += dX)
                {
                    Vector3d v = new Vector3d(x, y, 0.0);
                    Vector3d pv = new Vector3d(p);

                    if (axisAligned)
                    {
                        pv = new Vector3d(translationVector.X * dX, translationVector.Y * dY, 1);
                    }
                    Vector3d toVector = Vector3d.Add(v, pv);
                    Point3d pNew = new Point3d(toVector);

                    p = pNew;

                    PointContainment pc = boundary.Contains(p, Plane.WorldXY, 0.1);

                    if (pc == PointContainment.Inside)
                    {

                        // Translate rect to new position
                        Rectangle3d tRect = TranslateRect(p, rect);

                        // Check containment of rect
                        bool IsContained = BoundaryContainsCurve(tRect.ToNurbsCurve(), boundary);

                        if (!IsContained && withRotation && !searching)
                        {
                            tRect.Transform(Transform.Rotation(angle, p));
                            IsContained = BoundaryContainsCurve(tRect.ToNurbsCurve(), boundary);
                            IsRotated = true;
                        }

                        if (IsContained)
                        {
                            if (searching)
                            {
                                searching = false;
                                dX = spacing[0];
                                dY = spacing[1];
                                xMin = (1 - bx.Min.X % p.X) * p.X;
                            }
                            outPts.Add(p);
                            vs.Add(pv);
                            tRects.Add(tRect);

                        }
                        if (IsRotated && withRotation)
                        {
                            // Update spacing
                            double t = dX;
                            dX = dY;
                            dY = t;
                            IsRotated = false;
                        }
                    }
                }
            }

            return tRects;
        }

        private Rectangle3d TranslateRect(Point3d pt, Rectangle3d r0)
        {
            Vector3d c0 = new Vector3d(AreaMassProperties.Compute(r0.ToNurbsCurve()).Centroid);
            Vector3d tg = new Vector3d(pt);
            Vector3d t0 = Vector3d.Subtract(tg, c0);

            Rectangle3d rNew0 = new Rectangle3d(r0.Plane, r0.Width, r0.Height);
            rNew0.Transform(Transform.Translation(t0));

            return rNew0;
        }

        private bool BoundaryContainsCurve(Curve shape, Curve boundary)
        {
            bool IsContained = false;
            List<Point3d> vertices = shape.ToNurbsCurve().Points.Select(p => p.Location).ToList();

            for (int i = 0; i < vertices.Count; i++)
            {
                Point3d vertex = vertices[i];
                PointContainment pContainment = boundary.Contains(vertex, Plane.WorldXY, 0.00001);
                if (pContainment.Equals(PointContainment.Inside)) IsContained = true;
                else
                {
                    IsContained = false;
                    break;
                }
            }

            return IsContained;
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                //return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("0aff020c-20a1-4ae8-92d0-f7092210291c"); }
        }
    }
}
