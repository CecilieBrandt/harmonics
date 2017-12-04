using System;
using System.Collections.Generic;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Plankton;
using System.Drawing;

namespace Harmonics
{
    public class PlanktonTag : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the PlanktonTag class.
        /// </summary>
        public PlanktonTag()
          : base("PlanktonTag", "PlanktonTag",
              "Tagging of plankton mesh vertices, halfedges and faces",
              "Harmonics", "0 Mesh")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("PMesh", "PMesh", "The input PlanktonMesh to use the topology from", GH_ParamAccess.item);
            pManager.AddBooleanParameter("TagVertices", "v", "Toggle on vertex tagging", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("TagHalfedges", "he", "Toggle on halfedge tagging", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("TagFaces", "f", "Toggle on face tagging", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Location", "pt", "The tagging location", GH_ParamAccess.list);
            pManager.AddTextParameter("TextTag", "tag", "The text tag", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //Global variables
            PlanktonMesh pMesh = new PlanktonMesh();
            DA.GetData<PlanktonMesh>(0, ref pMesh);

            bool v = true;
            DA.GetData(1, ref v);

            bool he = true;
            DA.GetData(2, ref he);

            bool f = true;
            DA.GetData(3, ref f);

            //-----------------------------------------
            List<string> textTag = new List<string>();
            List<Point3d> posTag = new List<Point3d>();

            if (v)
            {
                textTag = verticesTextTag(pMesh);
                posTag = verticesPosTag(pMesh);
            }
            else if (he)
            {
                textTag = halfEdgesTextTag(pMesh);
                posTag = halfEdgesPosTag(pMesh);
            }
            else if (f)
            {
                textTag = facesTextTag(pMesh);
                posTag = facesPosTag(pMesh);
            }

            DA.SetDataList(0, posTag);
            DA.SetDataList(1, textTag);
        }

        //Vertices
        public List<string> verticesTextTag(PlanktonMesh pMesh)
        {
            List<string> verticesText = new List<string>();
            for (int i = 0; i < pMesh.Vertices.Count; i++)
            {
                verticesText.Add(i.ToString());
            }
            return verticesText;
        }

        public List<Point3d> verticesPosTag(PlanktonMesh pMesh)
        {
            List<Point3d> verticesPos = new List<Point3d>();

            foreach (PlanktonVertex pV in pMesh.Vertices)
            {
                verticesPos.Add(new Point3d(pV.X, pV.Y, pV.Z));
            }
            return verticesPos;
        }


        //Halfedges
        public List<string> halfEdgesTextTag(PlanktonMesh pMesh)
        {
            List<string> halfedgesText = new List<string>();
            for (int i = 0; i < pMesh.Halfedges.Count; i++)
            {
                halfedgesText.Add(i.ToString());
            }
            return halfedgesText;
        }

        public List<Point3d> halfEdgesPosTag(PlanktonMesh pMesh)
        {
            List<Point3d> halfedgesPos = new List<Point3d>();

            foreach (PlanktonHalfedge pHe in pMesh.Halfedges)
            {
                int startVertex = pHe.StartVertex;
                int endVertex = pMesh.Halfedges[pHe.NextHalfedge].StartVertex;

                Vector3d vecStart = new Vector3d(pMesh.Vertices[startVertex].X, pMesh.Vertices[startVertex].Y, pMesh.Vertices[startVertex].Z);
                Vector3d vecEnd = new Vector3d(pMesh.Vertices[endVertex].X, pMesh.Vertices[endVertex].Y, pMesh.Vertices[endVertex].Z);
                Vector3d vecEdge = Vector3d.Subtract(vecEnd, vecStart);
                Vector3d vecEdgeOneThird = Vector3d.Multiply(0.33, vecEdge);
                Vector3d vecPos = Vector3d.Add(vecStart, vecEdgeOneThird);
                halfedgesPos.Add(new Point3d(vecPos.X, vecPos.Y, vecPos.Z));
            }
            return halfedgesPos;
        }


        //Faces
        public List<string> facesTextTag(PlanktonMesh pMesh)
        {
            List<string> facesText = new List<string>();
            for (int i = 0; i < pMesh.Faces.Count; i++)
            {
                facesText.Add(i.ToString());
            }
            return facesText;
        }

        public List<Point3d> facesPosTag(PlanktonMesh pMesh)
        {
            List<Point3d> facesPos = new List<Point3d>();
            //PlanktonFace pF in pMesh.Faces
            for (int i = 0; i < pMesh.Faces.Count; i++)
            {
                Vector3d vecFaceCenter = new Vector3d(0, 0, 0);
                int[] faceVertices = pMesh.Faces.GetFaceVertices(i);
                foreach (int fVertex in faceVertices)
                {
                    PlanktonVertex pV = pMesh.Vertices[fVertex];
                    Vector3d vecVertex = new Vector3d(pV.X, pV.Y, pV.Z);
                    vecFaceCenter = Vector3d.Add(vecFaceCenter, vecVertex);
                }
                Vector3d vecPos = Vector3d.Divide(vecFaceCenter, faceVertices.Length);
                facesPos.Add(new Point3d(vecPos.X, vecPos.Y, vecPos.Z));
            }
            return facesPos;
        }


        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                return Properties.Resources.PMeshTaggingIconi;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("d094803b-ac03-47b2-b7d7-f64d6812df54"); }
        }
    }
}