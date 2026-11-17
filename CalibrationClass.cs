using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Media3D;

namespace Microsoft.Samples.Kinect.ControlsBasics
{
    /// <summary>Completed version of PartialCalibrationClass (same 3-step maths as the slides).</summary>
    public class CalibrationClass
    {
        private readonly KinectSensor m_kinectSensor;

        private readonly List<Point> m_calibPoints = new List<Point>();               //2d calibration points
        private readonly List<SkeletonPoint> m_skeletonCalibPoints = new List<SkeletonPoint>(); //3d skeleton points

        private Matrix3D m_groundPlaneTransform; //step 2 transform
        private Emgu.CV.Matrix<double> m_transform; //step 3 transform

        public bool IsCalibrated { get; private set; }

        public CalibrationClass(KinectSensor sensor)
        {
            m_kinectSensor = sensor;
        }

        public void Reset()
        {
            m_calibPoints.Clear();
            m_skeletonCalibPoints.Clear();
            IsCalibrated = false;
        }

        /// <summary>Store one (screen point, skeleton position) pair. Returns false if depth is invalid.</summary>
        public bool AddPoint(Point screenPoint, SkeletonPoint skeletonPoint)
        {
            if (m_calibPoints.Count >= 4) return false;
            if (conertSkeletonPointToDepthPoint(skeletonPoint).Z <= 0) return false;
            m_calibPoints.Add(screenPoint);
            m_skeletonCalibPoints.Add(skeletonPoint);
            return true;
        }

        /// <summary>Runs the calibration. Returns the max reprojection error in screen units.</summary>
        public double Calibrate()
        {
            if (m_skeletonCalibPoints.Count != 4 || m_calibPoints.Count != 4)
                throw new InvalidOperationException("Need exactly 4 calibration points");

            //seketon 3D positions --> 3d positions in depth camera
            Point3D p0 = conertSkeletonPointToDepthPoint(m_skeletonCalibPoints[0]);
            Point3D p1 = conertSkeletonPointToDepthPoint(m_skeletonCalibPoints[1]);
            Point3D p2 = conertSkeletonPointToDepthPoint(m_skeletonCalibPoints[2]);
            Point3D p3 = conertSkeletonPointToDepthPoint(m_skeletonCalibPoints[3]);

            //3d positions depth camera --> positions on a 2D plane
            Vector3D v1 = p1 - p0;
            v1.Normalize();

            Vector3D v2 = p2 - p0;
            v2.Normalize();

            Vector3D planeNormalVec = Vector3D.CrossProduct(v1, v2);
            planeNormalVec.Normalize();

            Vector3D resultingPlaneNormal = new Vector3D(0, 0, 1);
            m_groundPlaneTransform = Util.make_align_axis_matrix(resultingPlaneNormal, planeNormalVec);

            Point3D p0OnPlane = m_groundPlaneTransform.Transform(p0);
            Point3D p1OnPlane = m_groundPlaneTransform.Transform(p1);
            Point3D p2OnPlane = m_groundPlaneTransform.Transform(p2);
            Point3D p3OnPlane = m_groundPlaneTransform.Transform(p3);

            //2d plane positions --> exact 2d square on screen (using perspective transform)
            System.Drawing.PointF[] src = new System.Drawing.PointF[4];
            src[0] = new System.Drawing.PointF((float)p0OnPlane.X, (float)p0OnPlane.Y);
            src[1] = new System.Drawing.PointF((float)p1OnPlane.X, (float)p1OnPlane.Y);
            src[2] = new System.Drawing.PointF((float)p2OnPlane.X, (float)p2OnPlane.Y);
            src[3] = new System.Drawing.PointF((float)p3OnPlane.X, (float)p3OnPlane.Y);

            System.Drawing.PointF[] dest = new System.Drawing.PointF[4];
            for (int i = 0; i < 4; i++)
                dest[i] = new System.Drawing.PointF((float)m_calibPoints[i].X, (float)m_calibPoints[i].Y);

            Emgu.CV.Mat transform = Emgu.CV.CvInvoke.GetPerspectiveTransform(src, dest);

            m_transform = new Emgu.CV.Matrix<double>(transform.Rows, transform.Cols, transform.NumberOfChannels);
            transform.CopyTo(m_transform);
            IsCalibrated = true;

            //test: the results should be the same as the points in m_calibPoints
            double maxErr = 0;
            for (int i = 0; i < 4; i++)
            {
                Point r = KinectToProjectionPoint(m_skeletonCalibPoints[i]);
                maxErr = Math.Max(maxErr, (r - m_calibPoints[i]).Length);
            }
            return maxErr;
        }

        private Point3D conertSkeletonPointToDepthPoint(SkeletonPoint skeletonPoint)
        {
            DepthImagePoint imgPt = m_kinectSensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skeletonPoint, DepthImageFormat.Resolution640x480Fps30);

            return new Point3D(imgPt.X, imgPt.Y, imgPt.Depth);
        }

        /// <summary>Any 3D skeleton position --> 2D position on the stage (birds-eye).</summary>
        public Point KinectToProjectionPoint(SkeletonPoint point)
        {
            Point3D p = conertSkeletonPointToDepthPoint(point);

            Point3D pOnGroundPlane = m_groundPlaneTransform.Transform(p);

            System.Drawing.PointF[] testPoint = new System.Drawing.PointF[1];
            testPoint[0] = new System.Drawing.PointF((float)pOnGroundPlane.X, (float)pOnGroundPlane.Y);

            System.Drawing.PointF[] resultPoint = Emgu.CV.CvInvoke.PerspectiveTransform(testPoint, m_transform);

            return new Point(resultPoint[0].X, resultPoint[0].Y);
        }
    }
}
