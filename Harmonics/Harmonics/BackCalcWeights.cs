using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Plankton;
using Rhino.Geometry.Intersect;
using Grasshopper.Kernel.Special;

namespace Harmonics
{
    public class BackCalcWeights : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the BackCalcWeights class.
        /// </summary>
        public BackCalcWeights()
          : base("BackCalcWeights", "BackCalcWeights",
              "Back-calculate the weights and modes to achieve a target surface from the initial mesh",
              "Harmonics", "2 Harmonics")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("PMeshInitial", "PMeshInit", "The initial PlanktonMesh to use topology from", GH_ParamAccess.item);
            pManager.AddSurfaceParameter("TargetSurface", "targetSrf", "The target surface", GH_ParamAccess.item);
            pManager.AddVectorParameter("VertexNormals", "n", "The vertex normals", GH_ParamAccess.list);
            pManager.AddMatrixParameter("EigenvectorMatrix", "v", "The eigenvector matrix of the initial PlanktonMesh", GH_ParamAccess.item);
            pManager.AddIntegerParameter("EigsCount", "k", "The number of eigenvectors to be used for back-calculation", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Option", "opt", "0: k most significant eigenvectors, 1: first k eigenvectors", GH_ParamAccess.item, 0);
            pManager.AddBooleanParameter("Rounding", "round", "Round the weights and scale factor off to 2 decimals", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("PresetSliders", "preset", "If true, the sliders named (w0, w1, ..., wn, scale) are preset to the calculated values", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("EigsIndices", "vIndex", "The eigenvector indices", GH_ParamAccess.list);
            pManager.AddNumberParameter("BackCalculatedWeights", "w", "The back-calculated weights to approximate the target surface", GH_ParamAccess.list);
            pManager.AddNumberParameter("ScaleFactor", "scale", "The scale factor", GH_ParamAccess.item);
            pManager.AddNumberParameter("RootMeanSquare", "RMS", "The root mean square value to evaluate the approximation accuracy", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //Global variables
            PlanktonMesh pMeshOrig = new PlanktonMesh();
            DA.GetData(0, ref pMeshOrig);

            Surface targetSrf = null;
            DA.GetData(1, ref targetSrf);

            List<Vector3d> directions = new List<Vector3d>();
            DA.GetDataList(2, directions);

            Matrix mV = null;
            DA.GetData(3, ref mV);

            int k = 1;
            DA.GetData(4, ref k);
            if (k < 1)
            {
                k = 1;
            }
            else if (k > mV.ColumnCount)
            {
                k = mV.ColumnCount;
            }

            int opt = 0;
            DA.GetData(5, ref opt);
            if (opt < 0)
            {
                opt = 0;
            }
            else if (opt > 1)
            {
                opt = 1;
            }

            bool round = true;
            DA.GetData(6, ref round);

            bool preset = false;
            DA.GetData(7, ref preset);

            //--------------------------------------------------------

            //Distance signal
            List<double> distanceSignal = calcDistSignal(pMeshOrig, directions, targetSrf);

            //All weights
            List<double> weights = calcMHT(distanceSignal, mV);
            double scale = calcScaleFactor(weights);

            List<double> weightsN = calcNormalisedWeights(weights, scale, round);          //normalised by scale factor and possibly rounded


            //Sorting - eigenvector indices
            List<int> eigsVIndices = new List<int>();

            //k most significant eigenvectors
            if (opt == 0)
            {
                eigsVIndices = calcSignificantEigs(weightsN, k);
            }
            //first k eigenvectors
            else if (opt == 1)
            {
                for (int j = 0; j < k; j++)
                {
                    eigsVIndices.Add(j);
                }
            }


            //Extract the weights accordingly
            List<double> weightsNExtract = extractWeights(weightsN, eigsVIndices);


            //Possibly round scale factor
            if (round)
            {
                scale = Math.Round(scale, 2);
            }


            //RMS
            double rms = calcRMS(pMeshOrig, mV, eigsVIndices, weightsNExtract, scale, directions, distanceSignal);


            //Preset slider values
            if (preset)
            {
                List<String> nicknames = new List<string>();
                List<double> values = new List<double>();

                for (int i = 0; i < weightsNExtract.Count; i++)
                {
                    nicknames.Add(String.Format("w{0}", i));
                    values.Add(weightsNExtract[i]);
                }

                nicknames.Add("scale");
                values.Add(scale);

                presetSliders(nicknames, values);
            }


            //-------------------------------------------------------

            //Output
            DA.SetDataList(0, eigsVIndices);
            DA.SetDataList(1, weightsNExtract);
            DA.SetData(2, scale);
            DA.SetData(3, rms);
        }

        //Methods

        //Calculate distance signal from intersection(s) of rays with target surface
        public List<double> calcDistSignal(PlanktonMesh _orig, List<Vector3d> _directions, Surface _targetSrf)
        {
            //Number of vertices. The number of intersections have to equal this number otherwise error message
            int vertexCount = _orig.Vertices.Count;

            //List to contain intersection points
            List<Point3d> intersectionPts = new List<Point3d>();

            //List of distances
            List<double> distanceSignal = new List<double>();

            //Target surface as list due to Rayshoot arguments
            List<Surface> surface = new List<Surface>();
            surface.Add(_targetSrf);

            //List of vertices as 3d points
            List<Point3d> vertices = new List<Point3d>();
            for (int i = 0; i < vertexCount; i++)
            {
                vertices.Add(new Point3d(_orig.Vertices[i].X, _orig.Vertices[i].Y, _orig.Vertices[i].Z));
            }


            //Intersections (ordered according to vertex sorting)
            for (int k = 0; k < vertexCount; k++)
            {
                // Test closest point to surface. If zero distance then it already intersects and therefore add to list of intersections
                double u;
                double v;
                _targetSrf.ClosestPoint(vertices[k], out u, out v);

                Point3d ptOnSrf = _targetSrf.PointAt(u, v);
                double dist = vertices[k].DistanceTo(ptOnSrf);

                if (dist <= 0.01)
                {
                    intersectionPts.Add(vertices[k]);
                    distanceSignal.Add(0.0);
                }

                //Raytrace
                else
                {
                    Point3d[] intPts;

                    try
                    {
                        Ray3d rayPos = new Ray3d(vertices[k], _directions[k]);
                        intPts = Intersection.RayShoot(rayPos, surface, 1);
                        intersectionPts.Add(intPts[0]);
                        distanceSignal.Add(vertices[k].DistanceTo(intPts[0]));
                    }
                    catch
                    {
                        try
                        {
                            Ray3d rayNeg = new Ray3d(vertices[k], -1 * _directions[k]);
                            intPts = Intersection.RayShoot(rayNeg, surface, 1);
                            intersectionPts.Add(intPts[0]);
                            distanceSignal.Add(-1 * (vertices[k].DistanceTo(intPts[0])));
                        }
                        catch
                        {
                            //Output error message
                            this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, String.Format("Intersection failed for vertex {0} with the specified direction", k));
                        }
                    }
                }
            }

            return distanceSignal;
        }

 

        //Calculate the projection onto the eigenvector basis (one scalar pr. frequency/eigenvalue)
        //Also known as the Manifold Harmonics Transform (MHT)
        public List<double> calcMHT(List<double> signal, Matrix mV)
        {
            List<double> weights = new List<double>();

            int n = mV.RowCount;        //number of vertices

            //for each eigenvector, calculate the amount of dist in V, that is the amplitude of that specific wave in the total shape
            for (int i = 0; i < mV.ColumnCount; i++)
            {
                double innerProduct = 0.0;

                for (int j = 0; j < n; j++)
                {
                    innerProduct += signal[j] * mV[j, i];
                }

                weights.Add(innerProduct);
            }
            return weights;
        }


        //Calculate the scale factor from the list of weights
        public double calcScaleFactor(List<double> weights)
        {
            double wMin = weights.Min();
            double wMax = weights.Max();

            double scale = wMax;
            if (Math.Abs(wMin) > wMax)
            {
                scale = Math.Abs(wMin);
            }

            return scale;
        }


        //Calculate normalised weights such that the largest value is +-1.0
        public List<double> calcNormalisedWeights(List<double> weights, double scale, bool round)
        {
            List<double> normalisedWeights = new List<double>();
            foreach (double w in weights)
            {
                double val = w;
                if (scale != 0.0)
                {
                    val /= scale;
                }

                if (round)
                {
                    val = Math.Round(val, 2);
                }

                normalisedWeights.Add(val);
            }
            return normalisedWeights;
        }


        //Create a list of k most significant eigenvector indices (largest weights both positive and negative)
        public List<int> calcSignificantEigs(List<double> weightsN, int k)
        {
            //List of absolute weights
            List<double> weightsAbs = new List<double>();
            foreach (double w in weightsN)
            {
                weightsAbs.Add(Math.Abs(w));
            }

            //find indices for max absolute values
            List<int> weightsMaxIndices = new List<int>();

            while (weightsMaxIndices.Count < k)
            {
                double maxVal = weightsAbs.Max();
                int indexMax = weightsAbs.IndexOf(maxVal);

                //make sure not to add the same index twice and that the weight does not equal zero
                if (weightsMaxIndices.Contains(indexMax) == false && maxVal != 0.0)
                {
                    weightsMaxIndices.Add(indexMax);

                    //replace max value with zero since it is the smallest possible value (thereby the list length is preserved and no value taken twice)
                    weightsAbs[indexMax] = 0.0;
                }
                else
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, String.Format("Only {0} non-zero weights were found instead of the specified {1}", weightsMaxIndices.Count, k));
                    break;
                }
            }

            return weightsMaxIndices;
        }



        //Extract weights according to eigenvector indices
        public List<double> extractWeights(List<double> weightsN, List<int> eigsVIndices)
        {
            List<double> weightsExtract = new List<double>();

            foreach (int index in eigsVIndices)
            {
                weightsExtract.Add(weightsN[index]);
            }

            return weightsExtract;
        }



        //Preset slider values to calculated weights
        public void presetSliders(List<String> nicknames, List<double> values)
        {

            for (int i = 0; i < values.Count; i++)
            {

                foreach (IGH_DocumentObject obj in this.OnPingDocument().Objects)
                {
                    GH_NumberSlider slider = obj as GH_NumberSlider;
                    if (slider == null) { continue; }
                    if (slider.NickName.Equals(nicknames[i], StringComparison.OrdinalIgnoreCase))
                    {
                        slider.Slider.Value = (decimal)values[i];
                    }
                }
            }
        }


        //Calculate the root mean square (RMS) from target surface

        //Linear combination of eigenvectors
        public List<double> nodalLinearCombination(List<double> weights, Matrix mV, List<int> eigsIndices)
        {
            //A value per node
            List<double> nodalValues = new List<double>();

            for (int i = 0; i < mV.RowCount; i++)
            {
                double nValue = 0.0;
                for (int j = 0; j < weights.Count; j++)
                {
                    nValue += weights[j] * mV[i, eigsIndices[j]];
                }
                nodalValues.Add(nValue);
            }
            return nodalValues;
        }

        //Map to displacement
        public List<Vector3d> mapToDisplacements(List<double> nodalValues, List<Vector3d> displDir, double scale)
        {
            List<Vector3d> nodalDisplacements = new List<Vector3d>();

            for (int i = 0; i < nodalValues.Count; i++)
            {
                Vector3d dir = displDir[i];
                dir.Unitize();
                nodalDisplacements.Add(dir * (nodalValues[i] * scale));
            }
            return nodalDisplacements;
        }

        //RMS
        public double calcRMS(PlanktonMesh pMesh, Matrix mV, List<int> eigsIndices, List<double> weights, double scale, List<Vector3d> displDir, List<double> distanceSignal)
        {
            List<double> nodalValues = nodalLinearCombination(weights, mV, eigsIndices);
            List<Vector3d> vertexDisplacements = mapToDisplacements(nodalValues, displDir, scale);

            List<double> vertexDeviations = new List<double>();
            for (int i = 0; i < pMesh.Vertices.Count; i++)
            {
                Point3d vPos = new Point3d(pMesh.Vertices[i].X, pMesh.Vertices[i].Y, pMesh.Vertices[i].Z);
                Point3d vPosNew = vPos + vertexDisplacements[i];

                Point3d vPosSrf = vPos + displDir[i] * distanceSignal[i];

                double deviation = vPosNew.DistanceTo(vPosSrf);
                vertexDeviations.Add(deviation);
            }

            double rms = 0.0;
            foreach (double dev in vertexDeviations)
            {
                rms += Math.Pow(dev, 2);
            }
            double n = (double)vertexDeviations.Count;
            rms /= n;
            rms = Math.Sqrt(rms);
            return rms;
        }


        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                return Properties.Resources.BackCalculateWeightsIcon;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("2fcc1145-8ada-4cd9-8b45-dddd87c001b7"); }
        }
    }
}