using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace JupedsimSharp
{
    public class JupedsimSharpComponent : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        /// 

        public JupedsimSharpComponent()
          : base("Jupedsim Main Solver", "Main Slover",
            "Description",
            "JuPedSim", "Subcategory")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Boundary", "B", "Simulation boundary curve", GH_ParamAccess.item);
            pManager.AddPointParameter("Agent Points", "A", "Initial agent positions", GH_ParamAccess.list);
            pManager.AddCurveParameter("Exit Curves", "E", "Exit area curves", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Run", "R", "Run simulation", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Reset", "Re", "Reset simulation", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Final Positions", "FP", "Final agent positions", GH_ParamAccess.list);
            pManager.AddTextParameter("Status", "S", "Simulation status", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Curve boundaryCurve = null;
            List<Point3d> agentPoints = new List<Point3d>();
            List<Curve> exitCurves = new List<Curve>();
            bool runSimulation = false;
            bool reset = false;

            if (!DA.GetData(0, ref boundaryCurve)) return;
            if (!DA.GetDataList(1, agentPoints)) return;
            if (!DA.GetDataList(2, exitCurves)) return;
            if (!DA.GetData(3, ref runSimulation)) return;
            if (!DA.GetData(4, ref reset)) return;

            if (!initialized || reset)
            {
                if (simulation != IntPtr.Zero)
                {
                    JuPedSimAPI.JPS_Simulation_Free(simulation);
                }
                simulation = InitializeJuPedSim(boundaryCurve, exitCurves, agentPoints);
                initialized = true;
                finalPositions.Clear();
            }

            if (runSimulation && simulation != IntPtr.Zero)
            {
                IntPtr errorMessage;
                finalPositions.Clear();
                if (!JuPedSimAPI.JPS_Simulation_Iterate(simulation, out errorMessage))
                {
                    if (errorMessage != IntPtr.Zero)
                    {
                        var error = Marshal.PtrToStringAnsi(JuPedSimAPI.JPS_ErrorMessage_GetMessage(errorMessage));
                        JuPedSimAPI.JPS_ErrorMessage_Free(errorMessage);
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Simulation iteration failed: {error}");
                    }
                    return;
                }

                finalPositions = GetAgentPositions(simulation);
                DA.SetDataList(0, finalPositions);
                DA.SetData(1, "Simulation running...");
            }
            else
            {
                DA.SetDataList(0, finalPositions);
                DA.SetData(1, initialized ? "Simulation initialized" : "Not initialized");
            }
        }

        private bool initialized = false;
        private List<Point3d> finalPositions = new List<Point3d>();
        private IntPtr simulation;

        private IntPtr InitializeJuPedSim(Curve boundaryCurve, List<Curve> exitCurves, List<Point3d> agentPoints)
        {
            IntPtr errorMessage;

            IntPtr model_builder = JuPedSimAPI.JPS_CollisionFreeSpeedModelBuilder_Create(
                strengthNeighborRepulsion: 8.0,
                rangeNeighborRepulsion: 0.1,
                strengthGeometryRepulsion: 5.0,
                rangeGeometryRepulsion: 0.02);

            IntPtr model = JuPedSimAPI.JPS_CollisionFreeSpeedModelBuilder_Build(model_builder, out errorMessage);

            if (model == IntPtr.Zero)
            {
                if (errorMessage != IntPtr.Zero)
                {
                    var error = Marshal.PtrToStringAnsi(JuPedSimAPI.JPS_ErrorMessage_GetMessage(errorMessage));
                    JuPedSimAPI.JPS_ErrorMessage_Free(errorMessage);
                    throw new Exception($"Error creating model: {error}");
                }
                throw new Exception("Error creating model: Unknown error");
            }

            IntPtr geometry = CreateGeometry(boundaryCurve, exitCurves);
            if (geometry == IntPtr.Zero)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Error creating geometry.");
            }

            // Create simulation
            IntPtr simulation = JuPedSimAPI.JPS_Simulation_Create(model, geometry, 0.01, out errorMessage);

            JuPedSimAPI.JPS_OperationalModel_Free(model);
            JuPedSimAPI.JPS_Geometry_Free(geometry);

            if (simulation == IntPtr.Zero)
            {
                var errorString = Marshal.PtrToStringAnsi(errorMessage);
                JuPedSimAPI.JPS_ErrorMessage_Free(errorMessage);
                throw new Exception($"Error creating simulation: {errorString}");
            }

            // Exit Stage
            var exitPoints = ConvertCurveToPoints(exitCurves[0]);
            var exitId = JuPedSimAPI.JPS_Simulation_AddStageExit(
                simulation,
                exitPoints,
                (UIntPtr)exitPoints.Length,
                out _);

            // Journey
            var journey = JuPedSimAPI.JPS_JourneyDescription_Create();
            JuPedSimAPI.JPS_JourneyDescription_AddStage(journey, exitId);
            var journeyId = JuPedSimAPI.JPS_Simulation_AddJourney(simulation, journey, out _);

            // initialize agents
            foreach (var point in agentPoints)
            {
                AddAgentToSimulation(simulation, point, journeyId, exitId);
            }

            return simulation;
        }

        private static IntPtr CreateGeometry(Curve boundaryCurve, List<Curve> exitCurves)
        {
            var builder = JuPedSimAPI.JPS_GeometryBuilder_Create();

            // 转换边界曲线为点数组  
            var boundaryPoints = ConvertCurveToPoints(boundaryCurve);
            JuPedSimAPI.JPS_GeometryBuilder_AddAccessibleArea(builder, boundaryPoints, (UIntPtr)boundaryPoints.Length);

            IntPtr errorMessage;
            var geometry = JuPedSimAPI.JPS_GeometryBuilder_Build(builder, out errorMessage);

            JuPedSimAPI.JPS_GeometryBuilder_Free(builder);

            if (geometry == IntPtr.Zero)
            {
                var error = Marshal.PtrToStringAnsi(JuPedSimAPI.JPS_ErrorMessage_GetMessage(errorMessage));
                JuPedSimAPI.JPS_ErrorMessage_Free(errorMessage);
                throw new Exception($"Failed to create geometry: {error}");
            }

            return geometry;
        }

        private static void AddAgentToSimulation(IntPtr simulation, Point3d point, ulong journeyId, ulong exitId)
        {
            var parameters = new JuPedSimAPI.JPS_CollisionFreeSpeedModelAgentParameters
            {
                position = new JuPedSimAPI.JPS_Point { x = point.X, y = point.Y },
                journeyId = journeyId,
                stageId = exitId,  
                time_gap = 1.0,
                v0 = 1.2,
                radius = 0.3
            };

            IntPtr errorMessage;
            var agentId = JuPedSimAPI.JPS_Simulation_AddCollisionFreeSpeedModelAgent(simulation, parameters, out errorMessage);

            if (agentId == 0)
            {
                var error = Marshal.PtrToStringAnsi(JuPedSimAPI.JPS_ErrorMessage_GetMessage(errorMessage));
                JuPedSimAPI.JPS_ErrorMessage_Free(errorMessage);
                throw new Exception($"Failed to add agent: {error}");
            }
        }

        private static List<Point3d> GetAgentPositions(IntPtr simulation)
        {
            var positions = new List<Point3d>();
            var iterator = JuPedSimAPI.JPS_Simulation_AgentIterator(simulation);

            IntPtr agent;
            while ((agent = JuPedSimAPI.JPS_AgentIterator_Next(iterator)) != IntPtr.Zero)
            {
                var pos = JuPedSimAPI.JPS_Agent_GetPosition(agent);
                positions.Add(new Point3d(pos.x, pos.y, 0));
            }

            return positions;
        }

        private static JuPedSimAPI.JPS_Point[] ConvertCurveToPoints(Curve curve)
        {
            var points = new List<JuPedSimAPI.JPS_Point>();

            if (curve.TryGetPolyline(out Polyline polyline))
            {
                for(int i = 0; i < polyline.Count - 1; ++i)
                {
                    points.Add(new JuPedSimAPI.JPS_Point { x = polyline[i].X, y = polyline[i].Y });
                }
            }

            return points.ToArray();
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => null;

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("22aae030-ca65-4b2d-8306-945e2e0ec2b6");
    }
}