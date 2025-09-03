using System;
using System.Runtime.InteropServices;

namespace JupedsimSharp
{
    public static class JuPedSimAPI
    {
        const string DLL_NAME = "jupedsim.dll";

        // Basic Structure
        [StructLayout(LayoutKind.Sequential)]
        public struct JPS_Point
        {
            public double x;
            public double y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct JPS_CollisionFreeSpeedModelAgentParameters
        {
            public JPS_Point position;
            public ulong journeyId;
            public ulong stageId;
            public double time_gap;
            public double v0;
            public double radius;
        }

        // Simulation Management
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr JPS_Simulation_Create(
            IntPtr model,
            IntPtr geometry,
            double dT,
            out IntPtr errorMessage);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool JPS_Simulation_Iterate(
         IntPtr handle,
         out IntPtr errorMessage);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr JPS_Simulation_AgentCount(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void JPS_Simulation_Free(IntPtr handle);

        // 几何构建函数  
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr JPS_GeometryBuilder_Create();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void JPS_GeometryBuilder_AddAccessibleArea(
            IntPtr builder,
            [In] JPS_Point[] polygon,
            UIntPtr len_polygon);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr JPS_GeometryBuilder_Build(
            IntPtr builder,
            out IntPtr errorMessage);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void JPS_GeometryBuilder_Free(IntPtr builder);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr JPS_Geometry_Free(
            IntPtr geometry);

        // 模型构建函数  
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr JPS_CollisionFreeSpeedModelBuilder_Create(
            double strengthNeighborRepulsion,
            double rangeNeighborRepulsion,
            double strengthGeometryRepulsion,
            double rangeGeometryRepulsion);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr JPS_CollisionFreeSpeedModelBuilder_Build(
            IntPtr builder,
            out IntPtr errorMessage);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr JPS_OperationalModel_Free(
            IntPtr model);

        // 智能体管理函数  
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong JPS_Simulation_AddCollisionFreeSpeedModelAgent(
            IntPtr handle,
            JPS_CollisionFreeSpeedModelAgentParameters parameters,
            out IntPtr errorMessage);

        // 阶段和旅程管理  
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong JPS_Simulation_AddStageExit(
            IntPtr handle,
            JPS_Point[] polygon,
            UIntPtr len_polygon,
            out IntPtr errorMessage);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr JPS_JourneyDescription_Create();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void JPS_JourneyDescription_AddStage(
            IntPtr journey,
            ulong stageId);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong JPS_Simulation_AddJourney(
            IntPtr handle,
            IntPtr journey,
            out IntPtr errorMessage);

        // 智能体迭代器  
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr JPS_Simulation_AgentIterator(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr JPS_AgentIterator_Next(IntPtr iterator);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern JPS_Point JPS_Agent_GetPosition(IntPtr agent);

        // 错误处理函数  
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr JPS_ErrorMessage_GetMessage(IntPtr errorMessage);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void JPS_ErrorMessage_Free(IntPtr errorMessage);
    }
}