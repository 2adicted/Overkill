#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
#endregion

namespace Overkill
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        private Dictionary<Reference, Curve> detail_lines = new Dictionary<Reference, Curve>();

        private Dictionary<Reference, Curve> model_lines = new Dictionary<Reference, Curve>();

        private Dictionary<Reference, Curve> room_lines = new Dictionary<Reference, Curve>();

        private Dictionary<Reference, Curve> area_lines = new Dictionary<Reference, Curve>();

        private List<ElementId> survivor = new List<ElementId>();
        private List<ElementId> casualty = new List<ElementId>();

        public Result Execute(
          ExternalCommandData commandData,
          ref string message,
          ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            LineSelectionFilter filter = new LineSelectionFilter(doc);

            IList<Reference> references = uidoc.Selection.PickObjects(ObjectType.Element, filter, "Select multiple lines");

            if (null == references)
            {
                return Result.Cancelled;
            }


            foreach (Reference r in references)
            {
                Curve curve = (doc.GetElement(r).Location as LocationCurve).Curve;
                Line line = curve as Line;

                if (null == line) continue;

                string name = doc.GetElement(r).Category.Name;
                
                if (name.Equals("Detail Lines"))
                {
                    detail_lines.Add(r, (doc.GetElement(r).Location as LocationCurve).Curve);
                }
                else
                {
                    if (name.Equals("Lines"))
                    {
                        model_lines.Add(r, (doc.GetElement(r).Location as LocationCurve).Curve);
                    }
                    else if (name.Equals("<Room Separation>"))
                    {
                        room_lines.Add(r, (doc.GetElement(r).Location as LocationCurve).Curve);
                    }
                    else if (name.Equals("<Area Boundary>"))
                    {
                        area_lines.Add(r, (doc.GetElement(r).Location as LocationCurve).Curve);
                    }
                }
            }

            itterate(detail_lines, doc);
            itterate(model_lines, doc);
            itterate(room_lines, doc);
            itterate(area_lines, doc);

            using (Transaction t = new Transaction(doc, "Overkill"))
            {
                t.Start();
                doc.Delete(casualty);
                t.Commit();
            }

            TaskDialog.Show("Overkill", "Number of lines deleted: " + casualty.Count);

            return Result.Succeeded;
        }

        /// <summary>
        /// Goes through a 
        /// </summary>
        /// <param name="lines"></param>
        /// <param name="survivor"></param>
        /// <param name="casualty"></param>
        /// <param name="doc"></param>
        private void itterate(Dictionary<Reference, Curve> lines, Document doc)
        {
            int c = 0;

            foreach (KeyValuePair<Reference, Curve> a in lines)
            {
                if (casualty.Contains(a.Key.ElementId))
                {
                    c++;
                    continue;
                }

                foreach (KeyValuePair<Reference, Curve> b in lines)
                {
                    if (a.Equals(b))
                    {
                        c++;
                        continue;
                    }

                    if (casualty.Contains(b.Key.ElementId))
                    {
                        c++;
                        continue;
                    }

                    Tuple<KeyValuePair<Reference, Curve>, KeyValuePair<Reference, Curve>> l_tuple = Overlap(a, b, doc);

                    if (null != l_tuple)
                    {
                        survivor.Add(l_tuple.Item1.Key.ElementId);
                        casualty.Add(l_tuple.Item2.Key.ElementId);
                        continue;
                    }
                    else
                    {
                        survivor.Add(b.Key.ElementId);
                    }
                }
            }

        }
        /// <summary>
        /// Returns 
        /// </summary>
        /// <param name="l1"></param>
        /// <param name="l2"></param>
        /// <param name="doc"></param>
        /// <returns></returns>
        private Tuple<KeyValuePair<Reference, Curve>, KeyValuePair<Reference, Curve>> Overlap(KeyValuePair<Reference, Curve> l1, KeyValuePair<Reference, Curve> l2, Document doc)
        {
            double precision = 0.0001;

            XYZ l1p1 = l1.Value.GetEndPoint(0);
            XYZ l1p2 = l1.Value.GetEndPoint(1);
            XYZ l2p1 = l2.Value.GetEndPoint(0);
            XYZ l2p2 = l2.Value.GetEndPoint(1);

            XYZ v1 = l1p1 - l1p2;
            XYZ v2 = l2p1 - l2p2;

            XYZ check = l1p1 - l2p2;

            XYZ cross = v1.CrossProduct(v2);
            XYZ cross_check = v1.CrossProduct(check);   

            XYZ min1 = new XYZ(Math.Min(l1p1.X, l1p2.X),
                               Math.Min(l1p1.Y, l1p2.Y),
                               Math.Min(l1p1.Z, l1p2.Z));

            XYZ max1 = new XYZ(Math.Max(l1p1.X, l1p2.X),
                               Math.Max(l1p1.Y, l1p2.Y),
                               Math.Max(l1p1.Z, l1p2.Z));

            XYZ min2 = new XYZ(Math.Min(l2p1.X, l2p2.X),
                               Math.Min(l2p1.Y, l2p2.Y),
                               Math.Min(l2p1.Z, l2p2.Z));

            XYZ max2 = new XYZ(Math.Max(l2p1.X, l2p2.X),
                               Math.Max(l2p1.Y, l2p2.Y),
                               Math.Max(l2p1.Z, l2p2.Z));

            //			XYZ minIntersect = new XYZ(Math.Max(min1.X, min2.X), Math.Max(min1.Y, min2.Y), Math.Max(min1.Z, min2.Z));
            //			XYZ maxIntersect = new XYZ(Math.Min(max1.X, max2.X), Math.Min(max1.Y, max2.Y), Math.Min(max1.Z, max2.Z));
            //			
            //			XYZ minEnd = new XYZ(Math.Min(min1.X, min2.X), Math.Min(min1.Y, min2.Y), Math.Min(min1.Z, min2.Z));
            //			XYZ maxEnd = new XYZ(Math.Max(max1.X, max2.X), Math.Max(max1.Y, max2.Y), Math.Max(max1.Z, max2.Z));
            //			
            //			bool intersect = (minIntersect.X <= maxIntersect.X) && (minIntersect.Y <= maxIntersect.Y) && (minIntersect.Z <= maxIntersect.Z);

            bool contain = (((min1.X - min2.X) <= precision) && (max1.X - max2.X) >= -precision && (min1.Y - min2.Y) <= precision && (max1.Y - max2.Y) >= -precision && (min1.Z - min2.Z) <= precision && (max1.Z - max2.Z) >= -precision) ||
                (((min1.X - min2.X) >= -precision) && (max1.X - max2.X) <= precision && (min1.Y - min2.Y) >= -precision && (max1.Y - max2.Y) <= precision && (min1.Z - min2.Z) >= -precision && (max1.Z - max2.Z) <= precision);

            if (cross.IsZeroLength() && cross_check.IsZeroLength() && contain)
            {
                //				using (Transaction t = new Transaction(doc,"Overkill"))
                //	            {
                //	                t.Start();
                //	                (doc.GetElement(l1.Key).Location as LocationCurve).Curve = Line.CreateBound(maxEnd, minEnd);
                //	                t.Commit();
                //	            }				
                //				return Tuple.Create(l1,l2);

                return (v1.GetLength() > v2.GetLength()) ? Tuple.Create(l1, l2) : Tuple.Create(l2, l1);
            }

            else return null;
        }
    }

    /// <summary>
    /// Selection Filter that passes only lines  
    /// </summary>
    public class LineSelectionFilter : ISelectionFilter
    {
        Document doc = null;
        public LineSelectionFilter(Document document)
        {
            doc = document;
        }

        public bool AllowElement(Element element)
        {
            if (element.Category.Name == "Lines" ||
                element.Category.Name == "<Room Separation>" ||
                element.Category.Name == "<Area Boundary>")
            {
                return true;
            }
            return false;
        }

        public bool AllowReference(Reference refer, XYZ point)
        {
            return true;
        }
    }
}
