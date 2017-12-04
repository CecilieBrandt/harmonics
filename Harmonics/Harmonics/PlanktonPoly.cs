using System;
using System.Collections.Generic;
using Plankton;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Harmonics
{
    public class PlanktonPoly : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the PlanktonPoly class.
        /// </summary>
        public PlanktonPoly()
          : base("PlanktonPoly", "PlanktonPoly",
              "Create a Plankton mesh from vertices and face polygons (CCW)",
              "Harmonics", "0 Mesh")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("XYZ", "XYZ", "A list of vertices defined as point3D", GH_ParamAccess.list);
            pManager.AddCurveParameter("facePolygons", "ngon", "A list of face polygons created in CCW direction", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.Register_GenericParam("PlanktonMesh", "PMesh", "Plankton Mesh");
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //Global variables
            List<Point3d> list_pt_Vertices = new List<Point3d>();
            DA.GetDataList(0, list_pt_Vertices);

            List<Polyline> list_pl_FacePolygons = new List<Polyline>();

            //convert from curve to polyline since pManager only takes curves
            List<Curve> list_crv_FacePolygons = new List<Curve>();
            DA.GetDataList(1, list_crv_FacePolygons);
            foreach (Curve crv in list_crv_FacePolygons)
            {
                Polyline pl;
                crv.TryGetPolyline(out pl);
                list_pl_FacePolygons.Add(pl);
            }

            //Create plankton mesh
            PlanktonMesh pMesh = createPlanktonMesh(list_pt_Vertices, list_pl_FacePolygons);

            DA.SetData(0, pMesh);
        }

        //create plankton mesh from points and polygons
        public PlanktonMesh createPlanktonMesh(List<Point3d> list_pt_vertices, List<Polyline> list_pl_facePolygons)
        {
            //Create plankton mesh
            PlanktonMesh pMesh = new PlanktonMesh();

            //Add verticesXYZ
            foreach (Point3d pt in list_pt_vertices)
            {
                pMesh.Vertices.Add(pt.X, pt.Y, pt.Z);
            }

            //Add faces from polylines
            foreach (Polyline pl in list_pl_facePolygons)
            {
                List<Point3d> list_pt_face = new List<Point3d>();
                for (int i = 0; i < pl.Count - 1; i++)
                {
                    list_pt_face.Add(pl[i]);
                }

                //decrease accuracy to specified number of decimals
                int numDec = 3;
                List<int> list_int_faceIndices = new List<int>();

                for (int j = 0; j < list_pt_face.Count; j++)
                {
                    Point3d facePtNew = new Point3d(Math.Round(list_pt_face[j].X, numDec), Math.Round(list_pt_face[j].Y, numDec), Math.Round(list_pt_face[j].Z, numDec));

                    for (int k = 0; k < list_pt_vertices.Count; k++)
                    {
                        Point3d vertexPtNew = new Point3d(Math.Round(list_pt_vertices[k].X, numDec), Math.Round(list_pt_vertices[k].Y, numDec), Math.Round(list_pt_vertices[k].Z, numDec));
                        if (facePtNew.CompareTo(vertexPtNew) == 0)
                        {
                            list_int_faceIndices.Add(k);
                            break;
                        }
                    }
                }
                pMesh.Faces.AddFace(list_int_faceIndices);
            }
            return pMesh;
        }


        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                return Properties.Resources.PMeshFromPolylineIcon;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("a41da4d0-307f-4945-a2f8-d2d8ec4f5a61"); }
        }
    }
}