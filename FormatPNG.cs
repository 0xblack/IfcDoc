﻿// Name:        FormatPNG.cs
// Description: Image generation
// Author:      Tim Chipman
// Origination: Work performed for BuildingSmart by Constructivity.com LLC.
// Copyright:   (c) 2012 BuildingSmart International Ltd.
// License:     http://www.buildingsmart-tech.org/legal

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;

using IfcDoc.Schema.DOC;

namespace IfcDoc.Format.PNG
{
    class FormatPNG
    {
        const int CY = 12;  // height of each attribute
        const int CX = 200; // width of each entity and gap
        const int DX = 32;  // horizontal gap between each entity

        const double Factor = 0.375;// 0.375;

        /// <summary>
        /// Creates list of attributes for entity and supertypes.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        private static void BuildAttributeList(DocEntity entity, List<DocAttribute> list, Dictionary<string, DocObject> map)
        {
            DocObject docBase = null;
            if (entity.BaseDefinition != null && map.TryGetValue(entity.BaseDefinition, out docBase))
            {
                BuildAttributeList((DocEntity)docBase, list, map);
            }

            foreach (DocAttribute docAttr in entity.Attributes)
            {
                if (!String.IsNullOrEmpty(docAttr.Derived))
                {
                    // remove existing
                    foreach (DocAttribute exist in list)
                    {
                        if (exist.Name.Equals(docAttr.Name))
                        {
                            list.Remove(exist);
                            break;
                        }
                    }
                }
                else
                {
                    list.Add(docAttr);
                }
            }
        }

        private static void DrawAttribute(Graphics g, int lane, List<int> lanes, DocEntity docEntity, DocModelView docView, DocModelRuleAttribute ruleAttribute, Dictionary<string, DocObject> map, int offset, Dictionary<Rectangle, DocModelRule> layout, DocProject docProject)
        {
            int x = lane * CX;
            int y = lanes[lane];

            // find the index of the attribute
            List<DocAttribute> listAttr = new List<DocAttribute>();
            BuildAttributeList(docEntity, listAttr, map);

            int iAttr = -1;
            for (int i = 0; i < listAttr.Count; i++)
            {
                if (listAttr[i].Name.Equals(ruleAttribute.Name))
                {
                    // found it
                    iAttr = i;
                    break;
                }
            }

            if (iAttr >= 0)
            {
                DocAttribute docAttr = listAttr[iAttr];

                // map it
                foreach (DocModelRuleEntity ruleEntity in ruleAttribute.Rules)
                {
                    DocObject docObj = null;
                    if (map.TryGetValue(ruleEntity.Name, out docObj))
                    {
                        int dest = 0;
                        if (lanes.Count > lane + 1)
                        {
                            dest = lanes[lane + 1];
                        }

                        if (docObj is DocEntity)
                        {
                            DocEntity docEntityTarget = (DocEntity)docObj;

                            // resolve inverse attribute                        
                            List<DocAttribute> listTarget = new List<DocAttribute>();
                            BuildAttributeList(docEntityTarget, listTarget, map);
                            for (int i = 0; i < listTarget.Count; i++)
                            {
                                DocAttribute docAttrTarget = listTarget[i];
                                if (docAttr.Inverse != null && docAttrTarget.Name.Equals(docAttr.Inverse))
                                {
                                    // found it
                                    dest += CY * (i + 1);
                                    break;
                                }
                                else if (docAttrTarget.Inverse != null && docAttr.Name.Equals(docAttrTarget.Inverse))
                                {
                                    //...also need to check for type compatibility

                                    bool found = false;
                                    DocEntity docTest = docEntity;
                                    while (docTest != null)
                                    {
                                        if (docTest.Name.Equals(docAttrTarget.DefinedType))
                                        {
                                            found = true;
                                            break;
                                        }

                                        if (docTest.BaseDefinition == null)
                                            break;

                                        DocObject docBase = null;
                                        if (map.TryGetValue(docTest.BaseDefinition, out docBase))
                                        {
                                            docTest = docBase as DocEntity;
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }

                                    // found it
                                    if (found)
                                    {
                                        dest += CY * (i + 1);
                                        break;
                                    }
                                }
                            }

                            // draw the entity, recurse
                            DrawEntity(g, lane + 1, lanes, docEntityTarget, docView, null, ruleEntity, map, layout, docProject);
                        }
                        else
                        {
                            int targetY = lanes[lane + 1];

                            g.FillRectangle(Brushes.Black, x + CX, targetY, CX - DX, CY);
                            g.DrawRectangle(Pens.Black, x + CX, targetY, CX - DX, CY);
                            using(Font font = new Font(FontFamily.GenericSansSerif, 8.0f))
                            {
                                string content = docObj.Name;
                                foreach (DocModelRule ruleConstraint in ruleEntity.Rules)
                                {
                                    if (ruleConstraint.Description != null && ruleConstraint.Description.StartsWith("Value="))
                                    {
                                        content = ruleConstraint.Description.Substring(6);                                        

                                        using (StringFormat fmt = new StringFormat())
                                        {
                                            fmt.Alignment = StringAlignment.Far;
                                            g.DrawString(content, font, Brushes.White, new RectangleF(x + CX, targetY, CX - DX, CY), fmt);
                                        }
                                    }
                                }

                                g.DrawString(docObj.Name, font, Brushes.White, x + CX, targetY);
                            }

                            // record rectangle
                            if (layout != null)
                            {
                                layout.Add(new Rectangle(x + CX, targetY, CX - DX, CY), ruleEntity);
                            }

                            // increment lane offset for all lanes
                            int minlane = targetY + CY * 2;
                            int i = lane + 1;
                            if (lanes[i] < minlane)
                            {
                                lanes[i] = minlane;
                            }
                        }

                        // draw arrow

                        int x0 = x + CX - DX;
                        int y0 = y + CY * (iAttr + 1) + CY / 2;
                        int x1 = x + CX;
                        int y1 = dest + CY / 2;
                        int xM = x0 + DX / 2 - offset * 2;

                        g.DrawLine(Pens.Black, x0, y0, xM, y0);
                        g.DrawLine(Pens.Black, xM, y0, xM, y1);
                        g.DrawLine(Pens.Black, xM, y1, x1, y1);
                    }
                }
            }
        }

        /// <summary>
        /// Draws entity and recurses.
        /// </summary>
        /// <param name="g">Graphics device.</param>
        /// <param name="lane">Horizontal lane for which to draw the entity.</param>
        /// <param name="lanes">List of lanes left-to-right.</param>
        /// <param name="docEntity">The entity to draw.</param>
        /// <param name="docView">The model view for which to draw the entity.</param>
        /// <param name="docTemplate">The template to draw.</param>
        /// <param name="docRule">Optional rule for recursing.</param>
        /// <param name="map">Map of definitions.</param>
        /// <param name="layout">Optional layout to receive rectangles for building image map</param>
        private static void DrawEntity(
            Graphics g, 
            int lane, 
            List<int> lanes, 
            DocEntity docEntity,
            DocModelView docView,
            DocTemplateDefinition docTemplate, 
            DocModelRuleEntity docRule, 
            Dictionary<string, DocObject> map, 
            Dictionary<Rectangle, DocModelRule> layout,
            DocProject docProject)
        {
            List<DocAttribute> listAttr = new List<DocAttribute>();
            BuildAttributeList(docEntity, listAttr, map);

            while(lanes.Count < lane + 1)
            {
                int miny = 0;
                if (lanes.Count > lane)
                {
                    miny = lanes[lane];
                }

                lanes.Add(miny);
            }

            int x = lane * CX;
            int y = lanes[lane];

            g.FillRectangle(Brushes.Black, x, y, CX - DX, CY);
            g.DrawRectangle(Pens.Black, x, y, CX - DX, CY);
            using (Font font = new Font(FontFamily.GenericSansSerif, 8.0f, FontStyle.Bold))
            {
                g.DrawString(docEntity.Name, font, Brushes.White, x, y);

                if (docEntity.IsAbstract())
                {
                    using (StringFormat fmt = new StringFormat())
                    {
                        fmt.Alignment = StringAlignment.Far;
                        g.DrawString("(ABS)", font, Brushes.White, new RectangleF(x, y, CX - DX, CY), fmt);
                    }
                }
            }

            g.DrawRectangle(Pens.Black, x, y + CY, CX - DX, CY * listAttr.Count);
            using (Font font = new Font(FontFamily.GenericSansSerif, 8.0f, FontStyle.Regular))
            {
                for (int iAttr = 0; iAttr < listAttr.Count; iAttr++)
                {
                    DocAttribute docAttr = listAttr[iAttr];

                    string display = docAttr.GetAggregationExpression();
                    Brush brush = Brushes.Black;
                    if (docAttr.Inverse != null)
                    {
                        brush = Brushes.Gray;
                    }

                    g.DrawString(docAttr.Name, font, brush, x, y + CY * (iAttr + 1));
                    using (StringFormat fmt = new StringFormat())
                    {
                        fmt.Alignment = StringAlignment.Far;
                        g.DrawString(display, font, brush, new RectangleF(x, y + CY * (iAttr + 1), CX-DX, CY), fmt);
                    }
                }
            }
            
            // record rectangle
            if (layout != null)
            {
                layout.Add(new Rectangle(x, y, CX - DX, CY + CY * listAttr.Count), docRule);
            }

            SortedList<int, List<DocModelRuleAttribute>> mapAttribute = new SortedList<int, List<DocModelRuleAttribute>>();
            Dictionary<DocModelRuleAttribute, DocTemplateDefinition> mapTemplate = new Dictionary<DocModelRuleAttribute,DocTemplateDefinition>();
            if (docRule != null)
            {
                // map inner rules

                // sort
                foreach (DocModelRuleAttribute ruleAttribute in docRule.Rules)
                {
                    for (int i = 0; i < listAttr.Count; i++)
                    {
                        if (listAttr[i].Name.Equals(ruleAttribute.Name))
                        {
                            // found it
                            if (!mapAttribute.ContainsKey(i))
                            {
                                mapAttribute.Add(i, new List<DocModelRuleAttribute>());
                            }

                            mapAttribute[i].Add(ruleAttribute);
                            break;
                        }
                    }
                }
            }
            else if (docTemplate != null)
            {
                if (docTemplate.Rules != null)
                {
                    foreach (DocModelRuleAttribute ruleAttribute in docTemplate.Rules)
                    {
                        for (int i = 0; i < listAttr.Count; i++)
                        {
                            if (listAttr[i].Name.Equals(ruleAttribute.Name))
                            {
                                // found it
                                //iAttr = i;
                                if (!mapAttribute.ContainsKey(i))
                                {
                                    mapAttribute.Add(i, new List<DocModelRuleAttribute>());
                                }
                                mapAttribute[i].Add(ruleAttribute);
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                // map each use definition at top-level              


                // build from inherited entities too

                List<DocTemplateDefinition> listTemplates = new List<DocTemplateDefinition>(); // keep track of templates so we don't repeat at supertypes

                DocEntity docEntitySuper = docEntity;
                while(docEntitySuper != null)
                {

                    foreach (DocModelView docEachView in docProject.ModelViews)
                    {
                        if (docView == null || docView == docEachView)//docView.Visible)
                        {
                            foreach (DocConceptRoot docRoot in docView.ConceptRoots)
                            {
                                if (docRoot.ApplicableEntity == docEntitySuper)
                                {
                                    foreach (DocTemplateUsage docUsage in docRoot.Concepts)
                                    {
                                        if (docUsage.Definition != null && docUsage.Definition.Rules != null && !listTemplates.Contains(docUsage.Definition))
                                        {
                                            listTemplates.Add(docUsage.Definition);

                                            foreach (DocModelRuleAttribute ruleAttribute in docUsage.Definition.Rules)
                                            {
                                                for (int i = 0; i < listAttr.Count; i++)
                                                {
                                                    if (listAttr[i].Name.Equals(ruleAttribute.Name))
                                                    {
                                                        // found it                                
                                                        if (!mapAttribute.ContainsKey(i))
                                                        {
                                                            mapAttribute.Add(i, new List<DocModelRuleAttribute>());
                                                        }

                                                        mapAttribute[i].Add(ruleAttribute);
                                                        if (!mapTemplate.ContainsKey(ruleAttribute))
                                                        {
                                                            mapTemplate.Add(ruleAttribute, docUsage.Definition);
                                                        }
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    DocObject docTest = null;
                    if (docEntitySuper.BaseDefinition != null && map.TryGetValue(docEntitySuper.BaseDefinition, out docTest))
                    {
                        docEntitySuper = docTest as DocEntity;
                    }
                    else
                    {
                        docEntitySuper = null;
                    }
                }
            }

            int offset = -mapAttribute.Values.Count / 2;

            DocTemplateDefinition lastTemplate = null;
            foreach (List<DocModelRuleAttribute> listSort in mapAttribute.Values)
            {
                if (docRule == null && docTemplate == null)
                {
                    // offset all lanes
                    int maxlane = 0;
                    for (int i = 1; i < lanes.Count; i++)
                    {
                        if (lanes[i] > maxlane)
                        {
                            maxlane = lanes[i];
                        }
                    }

                    for (int i = 1; i < lanes.Count; i++)
                    {
                        lanes[i] = maxlane;
                    }
                }

                foreach (DocModelRuleAttribute ruleAttributeSort in listSort)
                {
                    // indicate each template
                    DocTemplateDefinition eachTemplate = null;
                    if (mapTemplate.TryGetValue(ruleAttributeSort, out eachTemplate))
                    {
                        // offset for use definition
                        int minlan = 0;
                        for (int i = 1; i < lanes.Count; i++)
                        {
                            if (eachTemplate != lastTemplate)
                            {
                                lanes[i] += CY * 2;
                            }

                            if (lanes[i] > minlan)
                            {
                                minlan = lanes[i];
                            }
                        }

                        // verify this...
                        for (int i = 1; i < lanes.Count; i++)
                        {
                            if (lanes[i] < minlan)
                            {
                                lanes[i] = minlan;
                            }
                        }

                        if (eachTemplate != lastTemplate)
                        {
                            using (Font font = new Font(FontFamily.GenericSansSerif, 8.0f, FontStyle.Italic))
                            {
                                g.DrawString(eachTemplate.Name, font, Brushes.Gray, CX, lanes[1] - CY * 2);
                            }
                            g.DrawLine(Pens.Gray, CX, lanes[1] - CY * 2, 1920, lanes[1] - CY * 2);
                        }

                        lastTemplate = eachTemplate;
                    }

                    DrawAttribute(g, lane, lanes, docEntity, docView, ruleAttributeSort, map, offset, layout, docProject);
                }
                offset++;
            }

            // increment lane offset
            int minlane = y + CY * (listAttr.Count + 2);            
            if (lanes[lane] < minlane)
            {
                lanes[lane] = minlane;
            }
            
        }

        /// <summary>
        /// Creates an entity diagram from use definitions.
        /// </summary>
        /// <param name="docEntity"></param>
        /// <returns></returns>
        internal static Image CreateEntityDiagram(DocEntity docEntity, DocModelView docView, Dictionary<string, DocObject> map, Dictionary<Rectangle, DocModelRule> layout, DocProject docProject)
        {
            Image image = new Bitmap(2048, 4096, System.Drawing.Imaging.PixelFormat.Format24bppRgb); //...TODO: adjust

            using (Graphics g = Graphics.FromImage(image))
            {
                g.FillRectangle(Brushes.White, new Rectangle(0, 0, image.Width, image.Height));

                List<int> lanes = new List<int>(); // keep track of position offsets in each lane
                for (int i = 0; i < 16; i++)
                {
                    lanes.Add(0);
                }

                DrawEntity(g, 0, lanes, docEntity, docView, null, null, map, layout, docProject);
            }

            // adjust size based on layout
            int indent = 8;
            Rectangle rcBounds = Rectangle.Empty;
            if (layout != null)
            {
                foreach (Rectangle rc in layout.Keys)
                {
                    if (rc.Right > rcBounds.Width)
                    {
                        rcBounds.Width = rc.Right;
                    }

                    if (rc.Bottom > rcBounds.Bottom)
                    {
                        rcBounds.Height = rc.Bottom;
                    }
                }

                rcBounds.Width += indent * 2;
                rcBounds.Height += indent * 2;
                Image imageClip = new Bitmap(rcBounds.Width, rcBounds.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(imageClip))
                {
                    g.FillRectangle(Brushes.White, rcBounds);
                    g.DrawImage(image, indent, indent);
                    g.DrawRectangle(Pens.Black, 0, 0, rcBounds.Width - 1, rcBounds.Height - 1);
                }

                image.Dispose();
                image = imageClip;
            }

            return image;
        }

        /// <summary>
        /// Creates an instance diagram for a template.
        /// </summary>
        /// <param name="docTemplate"></param>
        /// <returns></returns>
        internal static Image CreateTemplateDiagram(DocTemplateDefinition docTemplate, Dictionary<string, DocObject> map, Dictionary<Rectangle, DocModelRule> layout, DocProject docProject)
        {
            DocObject docTarget = null;
            if (docTemplate.Type == null || !map.TryGetValue(docTemplate.Type, out docTarget) || !(docTarget is DocEntity))
                return null;

            DocEntity docEntity = (DocEntity)docTarget;
            
            Image image = new Bitmap(1920, 1080, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            

            using (Graphics g = Graphics.FromImage(image))
            {
                g.FillRectangle(Brushes.White, new Rectangle(0, 0, image.Width, image.Height));

                List<int> lanes = new List<int>(); // keep track of position offsets in each lane
                for (int i = 0; i < 16; i++)
                {
                    lanes.Add(0);
                }

                DrawEntity(g, 0, lanes, docEntity, null, docTemplate, null, map, layout, docProject);
            }

            // adjust size based on layout
            int indent = 8;
            Rectangle rcBounds = Rectangle.Empty;
            if (layout != null)
            {
                foreach (Rectangle rc in layout.Keys)
                {
                    if (rc.Right > rcBounds.Width)
                    {
                        rcBounds.Width = rc.Right;
                    }

                    if (rc.Bottom > rcBounds.Bottom)
                    {
                        rcBounds.Height = rc.Bottom;
                    }
                }

                rcBounds.Width += indent * 2;
                rcBounds.Height += indent * 2;
                Image imageClip = new Bitmap(rcBounds.Width, rcBounds.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(imageClip))
                {
                    g.FillRectangle(Brushes.White, rcBounds);
                    g.DrawImage(image, indent, indent);
                    g.DrawRectangle(Pens.Black, 0, 0, rcBounds.Width - 1, rcBounds.Height - 1);
                }

                image.Dispose();
                image = imageClip;
            }

            return image;
        }

        private static void DrawRoundedRectangle(Graphics g, Rectangle rc, int radius, Pen pen, Brush brush)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rc.X, rc.Y, radius, radius, 180.0f, 90.0f);
            path.AddArc(rc.X + rc.Width - radius, rc.Y, radius, radius, 270.0f, 90.0f);
            path.AddArc(rc.X + rc.Width - radius, rc.Y + rc.Height - radius, radius, radius, 0.0f, 90.0f);
            path.AddArc(rc.X, rc.Y + rc.Height - radius, radius, radius, 90.0f, 90);
            path.CloseAllFigures();

            g.FillPath(brush, path);
            g.DrawPath(pen, path);
        }

        internal static Image CreateSchemaDiagram(DocSchema docSchema, Dictionary<string, DocObject> map)
        {
            float pageX = 600.0f;//?
            float pageY = 888.0f;//?

            int cDiagrams = docSchema.GetDiagramCount();
            int cPagesY = docSchema.DiagramPagesVert;
            int cPagesX = docSchema.DiagramPagesHorz;
            if(cPagesX == 0 || cPagesY == 0)
            {
                // fallback if using earlier version without diagram info
                cPagesX = cDiagrams;
                cPagesY = 1;

                if (cPagesX == 0)
                    cPagesX = 1;
            }

            int xTotal = cPagesX * (int)pageX;
            int yTotal = cPagesY * (int)pageY;
            Image image = new Bitmap(xTotal, yTotal, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            using (Graphics g = Graphics.FromImage(image))
            {
                Pen penDash = new Pen(System.Drawing.Color.Black);
                penDash.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                using (penDash)
                {
                    g.FillRectangle(Brushes.White, new Rectangle(0, 0, image.Width, image.Height));

                    for (float x = 0; x <= image.Width; x += pageX)
                    {
                        g.DrawLine(Pens.Green, new PointF(x-1, 0.0f), new PointF(x-1, (float)image.Height - 1.0f));
                        g.DrawLine(Pens.Green, new PointF(x, 0.0f), new PointF(x, (float)image.Height - 1.0f));
                    }
                    for (float y = 0; y <= image.Height; y += pageY)
                    {
                        g.DrawLine(Pens.Green, new PointF(0.0f, y-1), new PointF((float)image.Width - 1.0f, y-1));
                        g.DrawLine(Pens.Green, new PointF(0.0f, y), new PointF((float)image.Width - 1.0f, y));
                    }

                    StringFormat sf = new StringFormat(StringFormat.GenericDefault);
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;

                    using (Font font = new Font(FontFamily.GenericSansSerif, 7.0f))
                    {
                        using (Font fontBold = new Font(font, FontStyle.Bold))
                        {
                            foreach (DocType docType in docSchema.Types)
                            {
                                if (docType.DiagramRectangle != null)
                                {
                                    Rectangle rc = new Rectangle(
                                        (int)(docType.DiagramRectangle.X * Factor),
                                        (int)(docType.DiagramRectangle.Y * Factor),
                                        (int)(docType.DiagramRectangle.Width * Factor),
                                        (int)(docType.DiagramRectangle.Height * Factor));

                                    g.FillRectangle(Brushes.Lime, rc);
                                    g.DrawRectangle(penDash, rc);
                                    g.DrawString(docType.Name, font, Brushes.Black, rc, sf);

                                    if (docType is DocEnumeration)
                                    {
                                        g.DrawLine(penDash, rc.Right - 6, rc.Top, rc.Right - 6, rc.Bottom);
                                    }
                                    else if(docType is DocDefined)
                                    {
                                        DocDefined docItem = (DocDefined)docType;
                                        if (docItem.DiagramLine != null)
                                        {
                                            DrawLine(g, Pens.Black, docItem.DiagramLine);
                                        }
                                    }
                                    else if (docType is DocSelect)
                                    {
                                        g.DrawLine(penDash, rc.Left + 6, rc.Top, rc.Left + 6, rc.Bottom);

                                        DocSelect docSelect = (DocSelect)docType;

                                        if (docSelect.Tree != null)
                                        {
                                            foreach (DocLine docItem in docSelect.Tree)
                                            {
                                                if (docItem.Definition != null)
                                                {
                                                    DrawLine(g, Pens.Black, docItem.DiagramLine);
                                                }
                                                else
                                                {
                                                    // tree structure -- don't draw endcap
                                                    for (int i = 0; i < docItem.DiagramLine.Count - 1; i++)
                                                    {
                                                        g.DrawLine(Pens.Black,
                                                            new Point((int)(docItem.DiagramLine[i].X * Factor), (int)(docItem.DiagramLine[i].Y * Factor)),
                                                            new Point((int)(docItem.DiagramLine[i + 1].X * Factor), (int)(docItem.DiagramLine[i + 1].Y * Factor)));
                                                    }

                                                    foreach (DocLine docItem2 in docItem.Tree)
                                                    {
                                                        // link parent if necessary (needed for imported vex diagrams)
                                                        g.DrawLine(Pens.Black,
                                                            new Point((int)(docItem.DiagramLine[docItem.DiagramLine.Count - 1].X * Factor), (int)(docItem.DiagramLine[docItem.DiagramLine.Count - 1].Y * Factor)),
                                                            new Point((int)(docItem2.DiagramLine[0].X * Factor), (int)(docItem2.DiagramLine[0].Y * Factor)));

                                                        DrawLine(g, Pens.Black, docItem2.DiagramLine);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }


                            foreach (DocEntity docType in docSchema.Entities)
                            {
                                if (docType.DiagramRectangle != null)
                                {
                                    string caption = docType.Name;
                                    if (docType.WhereRules.Count > 0 || docType.UniqueRules.Count > 0)
                                    {
                                        caption = "*" + caption;
                                    }
                                    if (docType.IsAbstract())
                                    {
                                        caption = "(ABS)\r\n" + caption;
                                    }

                                    Rectangle rc = new Rectangle(
                                        (int)(docType.DiagramRectangle.X * Factor),
                                        (int)(docType.DiagramRectangle.Y * Factor),
                                        (int)(docType.DiagramRectangle.Width * Factor),
                                        (int)(docType.DiagramRectangle.Height * Factor));
                                    g.FillRectangle(Brushes.Yellow, rc);
                                    g.DrawRectangle(Pens.Black, rc);
                                    g.DrawString(caption, fontBold, Brushes.Black, rc, sf);
                                }

                                foreach (DocAttribute docAttr in docType.Attributes)
                                {
                                    if (docAttr.DiagramLine != null)
                                    {
                                        Pen pen = new Pen(System.Drawing.Color.Black);

                                        if (docAttr.IsOptional())
                                        {
                                            pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                                        }

                                        using (pen)
                                        {
                                            DrawLine(g, pen, docAttr.DiagramLine);
                                        }

                                    }

                                    if (docAttr.DiagramLabel != null && docAttr.DiagramLine != null)
                                    {
                                        string caption = docAttr.Name;
                                        if (!String.IsNullOrEmpty(docAttr.Inverse))
                                        {
                                            caption = "(" + docAttr.DefinedType + "." + docAttr.Inverse + ")\r\n(INV) " + caption;
                                        }
                                        if (docAttr.GetAggregation() != DocAggregationEnum.NONE)
                                        {
                                            caption += " " + docAttr.GetAggregationExpression();
                                        }

                                        // determine X/Y based on midpoint of stated coordinate and target attribute
                                        double x = (docAttr.DiagramLabel.X + docAttr.DiagramLine[docAttr.DiagramLine.Count - 1].X) * 0.5 * Factor;
                                        double y = docAttr.DiagramLabel.Y * Factor;
                                        g.DrawString(caption, font, Brushes.Black, (int)x, (int)y, sf);
                                    }
                                }

                                foreach (DocLine docSub in docType.Tree)
                                {
                                    DrawTree(g, docSub, Factor, Point.Empty);
                                }
                            }

                            if (docSchema.PageTargets != null)
                            {
                                using (Pen penRound = new Pen(Color.Black))
                                {
                                    penRound.StartCap = LineCap.Round;
                                    penRound.EndCap = LineCap.Round;
                                    int[] pagecounters = new int[64];

                                    foreach (DocPageTarget docTarget in docSchema.PageTargets)
                                    {
                                        pagecounters[docTarget.DiagramNumber]++;
                                        string caption = docTarget.Name;

                                        if (docTarget.DiagramRectangle != null)
                                        {
                                            Rectangle rc = new Rectangle(
                                                (int)(docTarget.DiagramRectangle.X * Factor),
                                                (int)(docTarget.DiagramRectangle.Y * Factor),
                                                (int)(docTarget.DiagramRectangle.Width * Factor),
                                                (int)(docTarget.DiagramRectangle.Height * Factor));
                                            DrawRoundedRectangle(g, rc, 32, penRound, Brushes.Silver);
                                            g.DrawString(caption, font, Brushes.Black, rc, sf);
                                        }

                                        if (docTarget.DiagramLine != null)
                                        {
                                            using (Pen penBlue = new Pen(Color.Blue, 2.0f))
                                            {
                                                for (int i = 0; i < docTarget.DiagramLine.Count - 1; i++)
                                                {
                                                    g.DrawLine(penBlue,
                                                        new Point((int)(docTarget.DiagramLine[i].X * Factor), (int)(docTarget.DiagramLine[i].Y * Factor)),
                                                        new Point((int)(docTarget.DiagramLine[i + 1].X * Factor), (int)(docTarget.DiagramLine[i + 1].Y * Factor)));
                                                }
                                            }
                                        }

                                        int iSource = 0;
                                        foreach (DocPageSource docSource in docTarget.Sources)
                                        {
                                            iSource++;
                                            if (docSource.DiagramRectangle != null)
                                            {
                                                Rectangle rc = new Rectangle(
                                                    (int)(docSource.DiagramRectangle.X * Factor),
                                                    (int)(docSource.DiagramRectangle.Y * Factor),
                                                    (int)(docSource.DiagramRectangle.Width * Factor),
                                                    (int)(docSource.DiagramRectangle.Height * Factor));
                                                DrawRoundedRectangle(g, rc, 32, penRound, Brushes.Silver);
                                                g.DrawString(docSource.Name, font, Brushes.Black, rc, sf);
                                            }
                                        }
                                    }
                                }
                            }

                            if (docSchema.SchemaRefs != null)
                            {
                                foreach (DocSchemaRef docSchemaRef in docSchema.SchemaRefs)
                                {
                                    foreach (DocDefinitionRef docDefRef in docSchemaRef.Definitions)
                                    {
                                        if (docDefRef.DiagramRectangle != null)
                                        {
                                            string caption = docSchemaRef.Name.ToUpper() + "." + docDefRef.Name;

                                            Rectangle rc = new Rectangle(
                                                (int)(docDefRef.DiagramRectangle.X * Factor),
                                                (int)(docDefRef.DiagramRectangle.Y * Factor),
                                                (int)(docDefRef.DiagramRectangle.Width * Factor),
                                                (int)(docDefRef.DiagramRectangle.Height * Factor));

                                            Rectangle rcInner = rc;
                                            rcInner.Y = rc.Y + rc.Height / 3;
                                            rcInner.Height = rc.Height / 3;

                                            g.FillRectangle(Brushes.Silver, rc);
                                            g.DrawRectangle(penDash, rc);
                                            DrawRoundedRectangle(g, rcInner, 8, Pens.Black, Brushes.Silver);
                                            g.DrawString(caption, font, Brushes.Black, rc, sf);

                                            foreach (DocLine docSub in docDefRef.Tree)
                                            {
                                                DrawTree(g, docSub, Factor, Point.Empty);
                                            }

                                        }

                                    }
                                }
                            }

                            if (docSchema.Comments != null)
                            {
                                using (Font fontItalic = new Font(font, FontStyle.Italic))
                                {
                                    foreach (DocComment docComment in docSchema.Comments)
                                    {
                                        if (docComment.DiagramRectangle != null)
                                        {
                                            Rectangle rc = new Rectangle(
                                                (int)(docComment.DiagramRectangle.X * Factor),
                                                (int)(docComment.DiagramRectangle.Y * Factor),
                                                (int)(docComment.DiagramRectangle.Width * Factor),
                                                (int)(docComment.DiagramRectangle.Height * Factor));
                                            g.DrawString(docComment.Documentation, fontItalic, Brushes.Blue, rc, sf);
                                        }
                                    }
                                }
                            }

                            if(docSchema.Primitives != null)
                            {
                                foreach(DocPrimitive docPrimitive in docSchema.Primitives)
                                {
                                    if (docPrimitive.DiagramRectangle != null)
                                    {
                                        Rectangle rc = new Rectangle(
                                            (int)(docPrimitive.DiagramRectangle.X * Factor),
                                            (int)(docPrimitive.DiagramRectangle.Y * Factor),
                                            (int)(docPrimitive.DiagramRectangle.Width * Factor),
                                            (int)(docPrimitive.DiagramRectangle.Height * Factor));

                                        g.FillRectangle(Brushes.Lime, rc);
                                        g.DrawRectangle(Pens.Black, rc);
                                        g.DrawString(docPrimitive.Name, font, Brushes.Black, rc, sf);

                                        g.DrawLine(Pens.Black, rc.Right - 6, rc.Top, rc.Right - 6, rc.Bottom);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return image;
        }

        private static void DrawLine(Graphics g, Pen pen, List<DocPoint> line)
        {
            for (int i = 0; i < line.Count - 1; i++)
            {
                Point ptA = new Point((int)(line[i].X * Factor), (int)(line[i].Y * Factor));
                Point ptB = new Point((int)(line[i + 1].X * Factor), (int)(line[i + 1].Y * Factor));
                g.DrawLine(pen, ptA, ptB);
                if (i == line.Count - 2)
                {
                    DrawEndCap(g, ptA, ptB);
                }
            }
        }

        private static void DrawEndCap(Graphics g, Point ptA, Point ptB)
        {
            int rad = 4;
            Rectangle rc = new Rectangle(ptB.X - rad, ptB.Y - rad, rad * 2, rad * 2);
            if (ptB.X > ptA.X)
            {
                rc.X -= rad;
            }
            else if (ptB.X < ptA.X)
            {
                rc.X += rad;
            }
            else if (ptB.Y > ptA.Y)
            {
                rc.Y -= rad;
            }
            else if (ptB.Y < ptA.Y)
            {
                rc.Y += rad;
            }
            g.FillEllipse(Brushes.White, rc);
            g.DrawEllipse(Pens.Black, rc);
        }

        private static void DrawTree(Graphics g, DocLine docSub, double factor, Point ptLast)
        {
            Point ptNext = Point.Empty;
            if (docSub.DiagramLine != null)
            {
                using (Pen pen = new Pen(Color.Black, 3.0f))
                {
                    for (int i = 0; i < docSub.DiagramLine.Count - 1; i++)
                    {
                        Point ptA = new Point((int)(docSub.DiagramLine[i].X * factor), (int)(docSub.DiagramLine[i].Y * factor));
                        Point ptB = new Point((int)(docSub.DiagramLine[i].X * factor), (int)(docSub.DiagramLine[i + 1].Y * factor));
                        Point ptC = new Point((int)(docSub.DiagramLine[i + 1].X * factor), (int)(docSub.DiagramLine[i + 1].Y * factor));

                        if (i == 0 && ptLast != Point.Empty)
                        {
                            g.DrawLine(pen, ptLast, ptA);
                        }
                        ptNext = ptC;

                        g.DrawLine(pen, ptA, ptB);
                        g.DrawLine(pen, ptB, ptC);
                    }
                }
            }

            foreach (DocLine docInner in docSub.Tree)
            {
                DrawTree(g, docInner, factor, ptNext);
            }
        }
    }
}
