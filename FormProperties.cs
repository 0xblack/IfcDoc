﻿// Name:        FormProperties.cs
// Description: Dialog for editing documentation, templates, concepts, and exchanges.
// Author:      Tim Chipman
// Origination: Work performed for BuildingSmart by Constructivity.com LLC.
// Copyright:   (c) 2011 BuildingSmart International Ltd.
// License:     http://www.buildingsmart-tech.org/legal

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

using IfcDoc.Schema.DOC;
using IfcDoc.Format.PNG;

namespace IfcDoc
{
    public partial class FormProperties : Form
    {
        DocObject[] m_path;
        DocObject m_target;
        DocObject m_parent; // parent object, used for filtering for templates
        DocProject m_project;
        Dictionary<string, DocObject> m_map;
        bool m_loadreq; // suppress updates of requirements while loading
        bool m_editcon; // suppress updates of concepts while moving or deleting
        bool m_loadagg;

        public FormProperties()
        {
            InitializeComponent();
        }

        //public FormProperties(DocObject docObject, DocObject docParent, DocProject docProject) : this()
        public FormProperties(DocObject[] path, DocProject docProject)
            : this()
        {
            this.m_path = path;
            this.m_target = path[path.Length-1];
            if (path.Length > 1)
            {
                this.m_parent = path[path.Length - 2];
            }
            this.m_project = docProject;
            this.m_map = new Dictionary<string, DocObject>();

            DocObject docObject = this.m_target;

            // build map
            foreach (DocSection docSection in this.m_project.Sections)
            {
                foreach (DocSchema docSchema in docSection.Schemas)
                {
                    foreach (DocEntity docEntity in docSchema.Entities)
                    {
                        this.m_map.Add(docEntity.Name, docEntity);
                    }

                    foreach (DocType docType in docSchema.Types)
                    {
                        this.m_map.Add(docType.Name, docType);
                    }
                }
            }                

            this.tabControl.TabPages.Clear();

            // General pages applies to all definitions
            this.tabControl.TabPages.Add(this.tabPageGeneral);

            this.textBoxGeneralName.Enabled = false;
            this.textBoxGeneralName.Text = docObject.Name;
            this.textBoxGeneralDescription.Text = docObject.Documentation;

            foreach (DocLocalization docLocal in docObject.Localization)
            {
                ListViewItem lvi = new ListViewItem();
                lvi.Tag = docLocal;
                lvi.Text = docLocal.Locale;
                lvi.SubItems.Add(docLocal.Name);
                lvi.SubItems.Add(docLocal.Documentation);
                this.listViewLocale.Items.Add(lvi);
            }

            this.tabControl.TabPages.Add(this.tabPageIdentity);
            this.textBoxIdentityUuid.Text = docObject.Uuid.ToString();
            this.textBoxIdentityCode.Text = docObject.Code;
            this.textBoxIdentityVersion.Text = docObject.Version;
            this.comboBoxIdentityStatus.Text = docObject.Status;
            this.textBoxIdentityAuthor.Text = docObject.Author;
            this.textBoxIdentityOwner.Text = docObject.Owner;
            this.textBoxIdentityCopyright.Text = docObject.Copyright;

            if (docObject is DocModelView)
            {
                this.tabControl.TabPages.Add(this.tabPageView);

                DocModelView docView = (DocModelView)docObject;
                if (docView.BaseView != null)
                {
                    this.textBoxViewBase.Text = docView.BaseView;
                    try
                    {
                        Guid guidView = new Guid(docView.BaseView);
                        DocModelView docViewBase = this.m_project.GetView(guidView);
                        if (docViewBase != null)
                        {
                            this.textBoxViewBase.Text = docViewBase.Name;
                        }
                    }
                    catch
                    {
                    }
                }

                if (docView.XsdFormats != null)
                {
                    foreach (DocXsdFormat docFormat in docView.XsdFormats)
                    {
                        ListViewItem lvi = new ListViewItem();
                        lvi.Tag = docFormat;
                        lvi.Text = docFormat.Entity;
                        lvi.SubItems.Add(docFormat.Attribute);
                        lvi.SubItems.Add(docFormat.XsdFormat.ToString());
                        lvi.SubItems.Add(docFormat.XsdTagless.ToString());

                        this.listViewViewXsd.Items.Add(lvi);
                    }
                }
            }
            else if (docObject is DocExchangeDefinition)
            {
                this.tabControl.TabPages.Add(this.tabPageExchange);

                DocExchangeDefinition docExchange = (DocExchangeDefinition)docObject;
                this.checkBoxExchangeImport.Checked = ((docExchange.Applicability & DocExchangeApplicabilityEnum.Import) != 0);
                this.checkBoxExchangeExport.Checked = ((docExchange.Applicability & DocExchangeApplicabilityEnum.Export) != 0);

                if (docExchange.Icon != null)
                {
                    try
                    {
                        this.panelIcon.BackgroundImage = Image.FromStream(new System.IO.MemoryStream(docExchange.Icon));
                    }
                    catch
                    {
                    }
                }

                this.comboBoxExchangeClassProcess.Text = docExchange.ExchangeClass;
                this.comboBoxExchangeClassSender.Text = docExchange.SenderClass;
                this.comboBoxExchangeClassReceiver.Text = docExchange.ReceiverClass;
            }
            else if (docObject is DocTemplateDefinition)
            {
                this.tabControl.TabPages.Add(this.tabPageTemplate);
                DocTemplateDefinition docTemplate = (DocTemplateDefinition)docObject;
                this.textBoxTemplateEntity.Text = docTemplate.Type;
                this.LoadTemplate();

                //this.tabControl.TabPages.Add(this.tabPageDiagram);
                //this.panelDiagram.BackgroundImage = FormatPNG.CreateTemplateDiagram(docTemplate, this.m_map, null, this.m_project);

                this.tabControl.TabPages.Add(this.tabPageUsage);
                foreach (DocModelView docView in this.m_project.ModelViews)
                {
                    foreach (DocConceptRoot docRoot in docView.ConceptRoots)
                    {
                        foreach (DocTemplateUsage docUsage in docRoot.Concepts)
                        {
                            if (docUsage.Definition == docTemplate)
                            {
                                DocObject[] usagepath = new DocObject[] { docRoot.ApplicableEntity, docRoot, docUsage };

                                ListViewItem lvi = new ListViewItem();
                                lvi.Tag = usagepath;
                                lvi.Text = docView.Name;
                                lvi.SubItems.Add(docRoot.ApplicableEntity.Name);
                                this.listViewUsage.Items.Add(lvi);
                            }
                        }
                    }
                }
            }
            else if (docObject is DocConceptRoot)
            {
                DocConceptRoot docRoot = (DocConceptRoot)docObject;
                DocEntity docEntity = (DocEntity)this.m_parent;
                DocModelView docView = null;

                // get view of root
                foreach (DocModelView docViewEach in this.m_project.ModelViews)
                {
                    if (docViewEach.ConceptRoots.Contains(docRoot))
                    {
                        docView = docViewEach;
                        break;
                    }
                }

                //this.tabControl.TabPages.Add(this.tabPageDiagram);
                //this.panelDiagram.BackgroundImage = FormatPNG.CreateEntityDiagram(docEntity, docView, this.m_map, null, this.m_project);
            }
            else if (docObject is DocTemplateUsage)
            {
                this.tabControl.TabPages.Add(this.tabPageConcept);
                this.tabControl.TabPages.Add(this.tabPageRequirements);

                DocTemplateUsage docUsage = (DocTemplateUsage)docObject;
                this.textBoxConceptTemplate.Text = docUsage.Definition.Name;
                this.checkBoxConceptOverride.Checked = docUsage.Override;
                this.LoadUsage();

                this.LoadModelView();
            }
            else if (docObject is DocSchema)
            {
                DocSchema docSchema = (DocSchema)docObject;

                // not yet ready
                //this.tabControl.TabPages.Add(this.tabPageDiagram);
                //this.panelDiagram.BackgroundImage = FormatPNG.CreateSchemaDiagram(docSchema, this.m_map);
            }
            else if (docObject is DocEntity)
            {
                DocEntity docEntity = (DocEntity)docObject;

                this.tabControl.TabPages.Add(this.tabPageEntity);
                this.textBoxEntityBase.Text = docEntity.BaseDefinition;
                this.checkBoxEntityAbstract.Checked = docEntity.IsAbstract();

                //this.tabControl.TabPages.Add(this.tabPageDiagram);
                //this.panelDiagram.BackgroundImage = FormatPNG.CreateEntityDiagram(docEntity, null, this.m_map, null, this.m_project);
            }
            else if (docObject is DocAttribute)
            {
                DocAttribute docAttribute = (DocAttribute)docObject;

                this.tabControl.TabPages.Add(this.tabPageAttribute);
                this.textBoxAttributeType.Text = docAttribute.DefinedType;
                this.textBoxAttributeInverse.Text = docAttribute.Inverse;

                this.checkBoxAttributeOptional.Checked = docAttribute.IsOptional();
                this.checkBoxXsdTagless.Checked = docAttribute.XsdTagless;
                this.comboBoxAttributeXsdFormat.SelectedItem = docAttribute.XsdFormat.ToString();

                this.LoadAttributeCardinality();
            }
            else if (docObject is DocConstraint)
            {
                DocConstraint docConstraint = (DocConstraint)docObject;

                this.tabControl.TabPages.Add(this.tabPageExpression);
                this.textBoxExpression.Text = docConstraint.Expression;
            }
            else if (docObject is DocPropertySet)
            {
                this.tabControl.TabPages.Add(this.tabPagePropertySet);

                DocPropertySet docPset = (DocPropertySet)docObject;
                this.LoadApplicability();

                this.comboBoxPsetType.Text = docPset.PropertySetType;
            }
            else if (docObject is DocProperty)
            {
                this.tabControl.TabPages.Add(this.tabPageProperty);

                DocProperty docProp = (DocProperty)docObject;
                this.comboBoxPropertyType.Text = docProp.PropertyType.ToString();
                this.textBoxPropertyData.Text = docProp.PrimaryDataType;

                if (!String.IsNullOrEmpty(docProp.SecondaryDataType))
                {
                    string[] enumhost = docProp.SecondaryDataType.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                    if (enumhost.Length == 2)
                    {
                        string[] enumvals = enumhost[1].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                        this.textBoxPropertyData.Text = enumhost[0];
                        this.listViewPropertyEnums.Items.Clear();
                        foreach (string eachenum in enumvals)
                        {
                            ListViewItem lvi = new ListViewItem();
                            lvi.Tag = eachenum;
                            lvi.Text = eachenum;
                            this.listViewPropertyEnums.Items.Add(lvi);
                        }
                    }
                }
            }
            else if (docObject is DocQuantitySet)
            {
                this.tabControl.TabPages.Add(this.tabPagePropertySet);
                this.LoadApplicability();
                this.comboBoxPsetType.Enabled = false;
            }
            else if (docObject is DocQuantity)
            {
                this.tabControl.TabPages.Add(this.tabPageQuantity);

                DocQuantity docProp = (DocQuantity)docObject;
                this.comboBoxQuantityType.Text = docProp.QuantityType.ToString();
            }
            else if (docObject is DocExample)
            {
                this.tabControl.TabPages.Add(this.tabPagePropertySet);
                this.LoadApplicability();
                this.comboBoxPsetType.Enabled = false;
                this.buttonApplicabilityAddTemplate.Visible = true;
            }
        }

        private void LoadApplicability()
        {
            DocVariableSet dvs = (DocVariableSet)this.m_target;

            this.listViewPsetApplicability.Items.Clear();

            if (!String.IsNullOrEmpty(dvs.ApplicableType))
            {
                string[] parts = dvs.ApplicableType.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string part in parts)
                {
                    string[] sub = part.Split('/');

                    ListViewItem lvi = new ListViewItem();
                    lvi.Tag = part;
                    lvi.Text = sub[0];

                    if (sub.Length > 1)
                    {
                        lvi.SubItems.Add(sub[1]);
                    }

                    this.listViewPsetApplicability.Items.Add(lvi);
                }
            }

            // templates
            if (dvs is DocExample)
            {
                DocExample dex = (DocExample)dvs;
                if (dex.ApplicableTemplates != null)
                {
                    foreach (DocTemplateDefinition dtd in dex.ApplicableTemplates)
                    {
                        ListViewItem lvi = new ListViewItem();
                        lvi.Tag = dtd;
                        lvi.Text = dtd.Name;

                        this.listViewPsetApplicability.Items.Add(lvi);
                    }
                }

                if (dex.ModelView != null)
                {
                    this.textBoxApplicabilityView.Text = dex.ModelView.Name;
                }
            }
        }

        private void LoadTemplate()
        {
            this.treeViewTemplateRules.Nodes.Clear();

            DocTemplateDefinition docTemplate = (DocTemplateDefinition)this.m_target;

            // add root rule according to applicable entity
            TreeNode tnRoot = new TreeNode();
            tnRoot.Tag = docTemplate;
            tnRoot.Text = docTemplate.Type;
            tnRoot.ImageIndex = 0;
            tnRoot.SelectedImageIndex = 0;
            tnRoot.ForeColor = Color.Gray; // top node is gray; cannot be edited

            this.treeViewTemplateRules.Nodes.Add(tnRoot);
            this.treeViewTemplateRules.SelectedNode = tnRoot;

            // load explicit rules
            if (docTemplate.Rules != null)
            {
                foreach (DocModelRule rule in docTemplate.Rules)
                {
                    this.LoadTreeRule(tnRoot, rule);
                }
            }

            this.treeViewTemplateRules.ExpandAll();
        }

        private void LoadModelView()
        {            
            this.listViewExchange.Items.Clear();

            // find the view
            DocModelView docView = null;
            DocConceptRoot docRoot = (DocConceptRoot)this.m_parent;
            foreach (DocModelView eachView in this.m_project.ModelViews)
            {
                if (eachView.ConceptRoots.Contains(docRoot))
                {
                    docView = eachView;
                    break;
                }
            }
            if (docView == null)
                return;

            DocTemplateUsage docUsage = (DocTemplateUsage)this.m_target;

            foreach (DocExchangeDefinition docExchange in docView.Exchanges)
            {
                DocExchangeRequirementEnum reqImport = DocExchangeRequirementEnum.NotRelevant;
                DocExchangeRequirementEnum reqExport = DocExchangeRequirementEnum.NotRelevant;

                // determine import/export support
                foreach (DocExchangeItem docExchangeItem in docUsage.Exchanges)
                {
                    if (docExchangeItem.Exchange == docExchange)
                    {
                        if (docExchangeItem.Applicability == DocExchangeApplicabilityEnum.Import)
                        {
                            reqImport = docExchangeItem.Requirement;
                        }
                        else if (docExchangeItem.Applicability == DocExchangeApplicabilityEnum.Export)
                        {
                            reqExport = docExchangeItem.Requirement;
                        }
                    }
                }
                
                ListViewItem lvi = new ListViewItem();
                lvi.Tag = docExchange;
                lvi.Text = docExchange.Name;
                lvi.SubItems.Add(reqImport.ToString());
                lvi.SubItems.Add(reqExport.ToString());
                this.listViewExchange.Items.Add(lvi);
            }
        }

        private void toolStripButtonInsertRuleAttribute_Click(object sender, EventArgs e)
        {
            DocModelRule rule = null;
            if (this.treeViewTemplateRules.SelectedNode != null)
            {
                rule = this.treeViewTemplateRules.SelectedNode.Tag as DocModelRule;
            }

            DocTemplateDefinition docTemplate = (DocTemplateDefinition)this.m_target;

            string typename = null;
            if (rule is DocModelRuleEntity)
            {
                DocModelRuleEntity docRuleEntity = (DocModelRuleEntity)rule;
                typename = docRuleEntity.Name;
            }
            else
            {
                // get applicable entity of target (or parent entity rule)
                typename = docTemplate.Type;
            }

            DocObject docobj = null;
            DocEntity docEntity = null;
            if (this.m_map.TryGetValue(typename, out docobj))
            {
                docEntity = this.m_map[typename] as DocEntity;
            }

            if (docEntity == null)
            {
                // launch dialog for constraint
                using (FormConstraint form = new FormConstraint())
                {
                    DialogResult res = form.ShowDialog(this);
                    if (res == DialogResult.OK)
                    {
                        DocModelRuleConstraint docRuleConstraint = new DocModelRuleConstraint();
                        rule.Rules.Add(docRuleConstraint);
                        docRuleConstraint.Description = form.Expression;
                        docRuleConstraint.Name = form.Expression; // for viewing

                        this.treeViewTemplateRules.SelectedNode = this.LoadTreeRule(this.treeViewTemplateRules.SelectedNode, docRuleConstraint);

                        // copy to child templates
                        docTemplate.PropagateRule(this.treeViewTemplateRules.SelectedNode.FullPath);
                    }
                }
            }
            else
            {
                // launch dialog to pick attribute of entity
                using (FormSelectAttribute form = new FormSelectAttribute(docEntity, this.m_map, null, true))
                {
                    DialogResult res = form.ShowDialog(this);
                    if (res == DialogResult.OK && form.Selection != null)
                    {
                        // then add and update tree
                        DocModelRuleAttribute docRuleAttr = new DocModelRuleAttribute();
                        docRuleAttr.Name = form.Selection;
                        if (rule != null)
                        {
                            rule.Rules.Add(docRuleAttr);
                        }
                        else
                        {
                            if (docTemplate.Rules == null)
                            {
                                docTemplate.Rules = new List<DocModelRule>();
                            }

                            docTemplate.Rules.Add(docRuleAttr);
                        }
                        this.treeViewTemplateRules.SelectedNode = this.LoadTreeRule(this.treeViewTemplateRules.SelectedNode, docRuleAttr);

                        // copy to child templates
                        docTemplate.PropagateRule(this.treeViewTemplateRules.SelectedNode.FullPath);
                    }
                }
            }
        }

        private void toolStripButtonInsertRuleEntity_Click(object sender, EventArgs e)
        {
            DocModelRuleAttribute rule = this.treeViewTemplateRules.SelectedNode.Tag as DocModelRuleAttribute;

            if(rule == null)
                return;

            DocTemplateDefinition docTemplate = (DocTemplateDefinition)this.m_target;

            // determine type of attribute by resolving attribute on type
            string typename = null;
            if (this.treeViewTemplateRules.SelectedNode.Parent != null && this.treeViewTemplateRules.SelectedNode.Parent.Tag is DocModelRuleEntity)
            {
                DocModelRuleEntity ruleEntity = (DocModelRuleEntity)this.treeViewTemplateRules.SelectedNode.Parent.Tag;
                typename = ruleEntity.Name;
            }
            else
            {
                // use base
                typename = docTemplate.Type;
            }
            DocEntity docEntity = (DocEntity)this.m_map[typename];
            
            // resolve attribute on entity
            DocAttribute docAttribute = FindAttribute(docEntity, rule.Name);
            if(docAttribute == null)
                return;

            // now get attribute type
            DocObject docobj = null;
            if (!this.m_map.TryGetValue(docAttribute.DefinedType, out docobj))
            {
                MessageBox.Show("The selected attribute is a value type and cannot be subtyped.");
            }
            else
            {
                // launch dialog to pick subtype of entity            
                using (FormSelectEntity form = new FormSelectEntity((DocDefinition)docobj, null, this.m_project))
                {
                    DialogResult res = form.ShowDialog(this);
                    if (res == DialogResult.OK && form.SelectedEntity != null)
                    {
                        // then add and update tree
                        DocModelRuleEntity docRuleAttr = new DocModelRuleEntity();
                        docRuleAttr.Name = form.SelectedEntity.Name;
                        rule.Rules.Add(docRuleAttr);
                        this.treeViewTemplateRules.SelectedNode = this.LoadTreeRule(this.treeViewTemplateRules.SelectedNode, docRuleAttr);

                        // copy to child templates
                        docTemplate.PropagateRule(this.treeViewTemplateRules.SelectedNode.FullPath);
                    }
                }
            }
        }

        private DocAttribute FindAttribute(DocEntity entity, string name)
        {
            foreach (DocAttribute eachattr in entity.Attributes)
            {
                if (eachattr.Name.Equals(name))
                    return eachattr;
            }

            // recurse
            if (entity.BaseDefinition != null)
            {
                DocEntity basetype = (DocEntity)this.m_map[entity.BaseDefinition];
                return FindAttribute(basetype, name);
            }

            return null; // not found
        }

        private void UpdateTreeRule(TreeNode tnRule)
        {
            DocModelRule docRule = (DocModelRule)tnRule.Tag;
            tnRule.Text = docRule.Name;

            DocTemplateDefinition docTemplateParent = this.m_parent as DocTemplateDefinition;
            if (docTemplateParent != null)
            {
                DocModelRule[] objpath = docTemplateParent.GetRulePath(tnRule.FullPath);
                if (objpath != null && objpath[objpath.Length - 1] != null)
                {
                    tnRule.ForeColor = Color.Gray;
                }
            }

            string tooltip = docRule.Name;
            // decorative text doesn't allow treeview path to work -- use tooltip in UI now instead
            tooltip += docRule.GetCardinalityExpression();
            if (!String.IsNullOrEmpty(docRule.Identification))
            {
                tooltip += " <" + docRule.Identification + ">";
                tnRule.BackColor = Color.LightBlue; // mark parameter
            }
            else
            {
                tnRule.BackColor = Color.Empty;
            }
            tnRule.ToolTipText = tooltip;
        }

        /// <summary>
        /// Loads rule into tree
        /// </summary>
        /// <param name="tnParent"></param>
        /// <param name="docRule"></param>
        /// <returns></returns>
        private TreeNode LoadTreeRule(TreeNode tnParent, DocModelRule docRule)
        {
            TreeNode tnRule = LoadNode(tnParent, docRule, docRule.Name);

            UpdateTreeRule(tnRule);

            foreach (DocModelRule docSub in docRule.Rules)
            {
                LoadTreeRule(tnRule, docSub);
            }

            return tnRule;
        }

        private TreeNode LoadNode(TreeNode parent, object tag, string text)
        {
            // if existing, then return
            foreach (TreeNode tnExist in parent.Nodes)
            {
                //if (tnExist.Text.Equals(text))
                //    return tnExist;
                if (tnExist.Tag == tag)
                    return tnExist;
            }

            TreeNode tn = new TreeNode();
            tn.Tag = tag;
            tn.Text = text;

            if (tag is DocModelRuleEntity)
            {
                tn.ImageIndex = 0;
                tn.SelectedImageIndex = 0;
            }
            else if (tag is DocModelRuleAttribute)
            {
                tn.ImageIndex = 1;
                tn.SelectedImageIndex = 1;
            }
            else if (tag is DocModelRuleConstraint)
            {
                tn.ImageIndex = 2;
                tn.SelectedImageIndex = 2;
            }

            parent.Nodes.Add(tn);

            return tn;
        }

        private void treeViewTemplateRules_AfterSelect(object sender, TreeViewEventArgs e)
        {
            TreeNode tnSelect = this.treeViewTemplateRules.SelectedNode;

            this.treeViewTemplateRules.Focus();

            bool locked = (tnSelect.ForeColor == Color.Gray);

            // update buttons
            DocModelRule rule = tnSelect.Tag as DocModelRule;
            this.buttonRuleDelete.Enabled = (rule != null) && !locked;
            this.buttonRuleAdd.Enabled = !(rule is DocModelRuleConstraint);
            this.buttonRuleUpdate.Enabled = (rule != null) && !locked;

            TreeNode tnParent = tnSelect.Parent;
            this.buttonMoveUp.Enabled = (tnParent != null && tnParent.Nodes.IndexOf(tnSelect) > 0) && !locked;
            this.buttonMoveDown.Enabled = (tnParent != null && tnParent.Nodes.IndexOf(tnSelect) < tnParent.Nodes.Count - 1) && !locked;
        }

        private void buttonTemplateEntity_Click(object sender, EventArgs e)
        {
            // determine base class from parent template if any

            string classname = null;// now can support any entity "IfcRoot";
            if (this.m_parent is DocTemplateDefinition)
            {
                classname = ((DocTemplateDefinition)this.m_parent).Type;
            }

            DocObject docobj = null;
            DocEntity docEntity = null;
            if (classname != null)
            {
                if (this.m_map.TryGetValue(classname, out docobj))
                {
                    docEntity = (DocEntity)docobj;
                }
            }

            // get selected entity
            DocTemplateDefinition docTemplate = (DocTemplateDefinition)this.m_target;
            DocObject target = null;
            DocEntity entity = null;
            if (docTemplate.Type != null && m_map.TryGetValue(docTemplate.Type, out target))
            {
                entity = (DocEntity)target;
            }

            using (FormSelectEntity form = new FormSelectEntity(docEntity, entity, this.m_project))
            {
                DialogResult res = form.ShowDialog(this);
                if (res == DialogResult.OK && form.SelectedEntity != null)
                {   
                    docTemplate.Type = form.SelectedEntity.Name;
                    this.textBoxTemplateEntity.Text = docTemplate.Type;
                }
            }

            this.LoadTemplate();
        }

        private void buttonConceptTemplate_Click(object sender, EventArgs e)
        {
            DocTemplateUsage docUsage = (DocTemplateUsage)this.m_target;
            DocConceptRoot docConceptRoot = (DocConceptRoot)this.m_parent;
            using (FormSelectTemplate form = new FormSelectTemplate(docUsage.Definition, this.m_project, docConceptRoot.ApplicableEntity))
            {
                if (form.ShowDialog(this) == DialogResult.OK && form.SelectedTemplate != null)
                {                    
                    docUsage.Definition = form.SelectedTemplate;
                    docUsage.Items.Clear();

                    this.textBoxConceptTemplate.Text = docUsage.Definition.Name;
                    this.LoadUsage();
                }
            }
        }

        private void LoadUsage()
        {
            this.dataGridViewConceptRules.Rows.Clear();
            this.dataGridViewConceptRules.Columns.Clear();

            DocTemplateUsage docUsage = (DocTemplateUsage)this.m_target;
            string[] parmnames = docUsage.Definition.GetParameterNames();
            foreach (string parmname in parmnames)
            {
                DataGridViewColumn column = new DataGridViewColumn();
                column.HeaderText = parmname;
                column.ValueType = typeof(string);//?
                column.CellTemplate = new DataGridViewTextBoxCell();
                column.Width = 200;

                // override cell template for special cases
                DocConceptRoot docConceptRoot = (DocConceptRoot)this.m_parent;
                DocEntity docEntity = docConceptRoot.ApplicableEntity;
                foreach (DocModelRuleAttribute docRule in docUsage.Definition.Rules)
                {
                    DocDefinition docDef = docEntity.ResolveParameterType(docRule, parmname, m_map);
                    if (docDef is DocEnumeration)
                    {
                        DocEnumeration docEnum = (DocEnumeration)docDef;
                        DataGridViewComboBoxCell cell = new DataGridViewComboBoxCell();
                        cell.MaxDropDownItems = 32;
                        cell.DropDownWidth = 200;
                        // add blank item
                        cell.Items.Add(String.Empty);
                        foreach (DocConstant docConst in docEnum.Constants)
                        {
                            cell.Items.Add(docConst.Name);
                        }
                        column.CellTemplate = cell;
                    }
                    else if (docDef is DocEntity || docDef is DocSelect)
                    {
                        // button to launch dialog for picking entity
                        DataGridViewButtonCell cell = new DataGridViewButtonCell();
                        cell.Tag = docDef;
                        column.CellTemplate = cell;
                    }
                }

                this.dataGridViewConceptRules.Columns.Add(column);
            }

            // add description column
            DataGridViewColumn coldesc = new DataGridViewColumn();
            coldesc.HeaderText = "Description";
            coldesc.ValueType = typeof(string);//?
            coldesc.CellTemplate = new DataGridViewTextBoxCell();
            coldesc.Width = 400;
            this.dataGridViewConceptRules.Columns.Add(coldesc);

            foreach (DocTemplateItem item in docUsage.Items)
            {
                string[] values = new string[this.dataGridViewConceptRules.Columns.Count];

                for(int i = 0; i < parmnames.Length; i++)
                {
                    string parmname = parmnames[i];
                    string val = item.GetParameterValue(parmname);
                    if (val != null)
                    {
                        values[i] = val;
                    }
                }

                values[values.Length - 1] = item.Documentation;

                int row = this.dataGridViewConceptRules.Rows.Add(values);
                this.dataGridViewConceptRules.Rows[row].Tag = item;
            }

            if (this.dataGridViewConceptRules.SelectedCells.Count > 0)
            {
                this.dataGridViewConceptRules.SelectedCells[0].Selected = false;
            }
        }

        private void dataGridViewConceptRules_CellValidated(object sender, DataGridViewCellEventArgs e)
        {
            if (this.m_editcon)
                return;

            // format parameters

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < this.dataGridViewConceptRules.Columns.Count - 1; i++)
            {                
                object val = this.dataGridViewConceptRules[i, e.RowIndex].Value;
                if (val != null)
                {
                    DataGridViewColumn col = this.dataGridViewConceptRules.Columns[i];
                    sb.Append(col.HeaderText);
                    sb.Append("=");
                    sb.Append(val as string);
                    sb.Append(";");
                }
            }

            DocTemplateUsage docUsage = (DocTemplateUsage)this.m_target;
            if (docUsage.Items.Count > e.RowIndex)
            {
                DocTemplateItem docItem = docUsage.Items[e.RowIndex];
                docItem.RuleParameters = sb.ToString();
                object val = this.dataGridViewConceptRules[this.dataGridViewConceptRules.Columns.Count - 1, e.RowIndex].Value;
                docItem.Documentation = val as string;
            }
        }

        private void dataGridViewConceptRules_UserAddedRow(object sender, DataGridViewRowEventArgs e)
        {
            DocTemplateUsage docUsage = (DocTemplateUsage)this.m_target;
            docUsage.Items.Add(new DocTemplateItem());
        }

        private void dataGridViewConceptRules_UserDeletedRow(object sender, DataGridViewRowEventArgs e)
        {
            DocTemplateUsage docUsage = (DocTemplateUsage)this.m_target;
            docUsage.Items.Remove((DocTemplateItem)e.Row.Tag);
        }

        private void listViewExchange_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.listViewExchange.SelectedItems.Count == 0)
            {
                // disable all
                this.radioButtonImportNone.Enabled = false;
                this.radioButtonImportExcluded.Enabled = false;
                this.radioButtonImportOptional.Enabled = false;
                this.radioButtonImportMandatory.Enabled = false;
                this.radioButtonExportNone.Enabled = false;
                this.radioButtonExportExcluded.Enabled = false;
                this.radioButtonExportOptional.Enabled = false;
                this.radioButtonExportMandatory.Enabled = false;
                return;
            }

            this.radioButtonImportNone.Enabled = true;
            this.radioButtonImportExcluded.Enabled = true;
            this.radioButtonImportOptional.Enabled = true;
            this.radioButtonImportMandatory.Enabled = true;
            this.radioButtonExportNone.Enabled = true;
            this.radioButtonExportExcluded.Enabled = true;
            this.radioButtonExportOptional.Enabled = true;
            this.radioButtonExportMandatory.Enabled = true;

            LoadExchangeRequirement(this.radioButtonImportNone, DocExchangeApplicabilityEnum.Import, DocExchangeRequirementEnum.NotRelevant);
            LoadExchangeRequirement(this.radioButtonImportExcluded, DocExchangeApplicabilityEnum.Import, DocExchangeRequirementEnum.Excluded);
            LoadExchangeRequirement(this.radioButtonImportOptional, DocExchangeApplicabilityEnum.Import, DocExchangeRequirementEnum.Optional);
            LoadExchangeRequirement(this.radioButtonImportMandatory, DocExchangeApplicabilityEnum.Import, DocExchangeRequirementEnum.Mandatory);
            LoadExchangeRequirement(this.radioButtonExportNone, DocExchangeApplicabilityEnum.Export, DocExchangeRequirementEnum.NotRelevant);
            LoadExchangeRequirement(this.radioButtonExportExcluded, DocExchangeApplicabilityEnum.Export, DocExchangeRequirementEnum.Excluded);
            LoadExchangeRequirement(this.radioButtonExportOptional, DocExchangeApplicabilityEnum.Export, DocExchangeRequirementEnum.Optional);
            LoadExchangeRequirement(this.radioButtonExportMandatory, DocExchangeApplicabilityEnum.Export, DocExchangeRequirementEnum.Mandatory);
        }

        private void LoadExchangeRequirement(RadioButton button, DocExchangeApplicabilityEnum applicability, DocExchangeRequirementEnum requirement)
        {
            DocTemplateUsage docUsage = (DocTemplateUsage)this.m_target;

            bool? common = null; // the common value
            bool varies = false; // whether value varies among objects

            foreach (ListViewItem lvi in this.listViewExchange.SelectedItems)
            {
                DocExchangeDefinition docDef = (DocExchangeDefinition)lvi.Tag;

                // find exchange on usage
                foreach (DocExchangeItem docItem in docUsage.Exchanges)
                {
                    if (docItem.Exchange == docDef && docItem.Applicability == applicability)
                    {
                        bool eachval = (docItem.Requirement == requirement);
                        if (common == null)
                        {
                            common = eachval;
                        }
                        else if (common != eachval)
                        {
                            varies = true;
                        }
                    }
                }
            }

            this.m_loadreq = true;
            button.Checked = (common == true && !varies);
            this.m_loadreq = false;
        }

        private void ApplyExchangeRequirement(DocExchangeApplicabilityEnum applicability, DocExchangeRequirementEnum requirement)
        {
            if (m_loadreq)
                return;

            // commit changes

            DocTemplateUsage docUsage = (DocTemplateUsage)this.m_target;

            foreach (ListViewItem lvi in this.listViewExchange.SelectedItems)
            {
                DocExchangeDefinition docExchange = (DocExchangeDefinition)lvi.Tag;

                // find existing  
                bool exists = false;
                foreach (DocExchangeItem docItem in docUsage.Exchanges)
                {
                    if (docItem.Exchange == docExchange && docItem.Applicability == applicability)
                    {
                        // found it
                        if (requirement == DocExchangeRequirementEnum.NotRelevant)
                        {
                            // delete item (reduce size)
                            docUsage.Exchanges.Remove(docItem);
                            docItem.Delete();
                        }
                        else
                        {
                            // update item
                            docItem.Requirement = requirement;
                        }
                        exists = true;
                        break; // perf, and collection may have been modified
                    }
                }

                if (!exists)
                {
                    DocExchangeItem docItem = new DocExchangeItem();
                    docItem.Exchange = docExchange;
                    docItem.Applicability = applicability;
                    docItem.Requirement = requirement;
                    docUsage.Exchanges.Add(docItem);
                }

                // update list
                if (applicability == DocExchangeApplicabilityEnum.Import)
                {
                    lvi.SubItems[1].Text = requirement.ToString();
                }
                else if (applicability == DocExchangeApplicabilityEnum.Export)
                {
                    lvi.SubItems[2].Text = requirement.ToString();
                }
            }            
        }

        private void radioButtonImportNone_CheckedChanged(object sender, EventArgs e)
        {
            ApplyExchangeRequirement(DocExchangeApplicabilityEnum.Import, DocExchangeRequirementEnum.NotRelevant);
        }

        private void radioButtonImportExcluded_CheckedChanged(object sender, EventArgs e)
        {
            ApplyExchangeRequirement(DocExchangeApplicabilityEnum.Import, DocExchangeRequirementEnum.Excluded);
        }

        private void radioButtonImportOptional_CheckedChanged(object sender, EventArgs e)
        {
            ApplyExchangeRequirement(DocExchangeApplicabilityEnum.Import, DocExchangeRequirementEnum.Optional);
        }

        private void radioButtonImportMandatory_CheckedChanged(object sender, EventArgs e)
        {
            ApplyExchangeRequirement(DocExchangeApplicabilityEnum.Import, DocExchangeRequirementEnum.Mandatory);
        }

        private void radioButtonExportNone_CheckedChanged(object sender, EventArgs e)
        {
            ApplyExchangeRequirement(DocExchangeApplicabilityEnum.Export, DocExchangeRequirementEnum.NotRelevant);
        }

        private void radioButtonExportExcluded_CheckedChanged(object sender, EventArgs e)
        {
            ApplyExchangeRequirement(DocExchangeApplicabilityEnum.Export, DocExchangeRequirementEnum.Excluded);
        }

        private void radioButtonExportOptional_CheckedChanged(object sender, EventArgs e)
        {
            ApplyExchangeRequirement(DocExchangeApplicabilityEnum.Export, DocExchangeRequirementEnum.Optional);
        }

        private void radioButtonExportMandatory_CheckedChanged(object sender, EventArgs e)
        {
            ApplyExchangeRequirement(DocExchangeApplicabilityEnum.Export, DocExchangeRequirementEnum.Mandatory);
        }

        private void listViewLocale_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.listViewLocale.SelectedItems.Count > 1)
            {
                this.textBoxGeneralName.Enabled = true;
                this.textBoxGeneralName.Text = "";
                this.textBoxGeneralDescription.Text = "";
                this.comboBoxLocaleCategory.SelectedIndex = -1;
                this.textBoxLocaleURL.Text = "";
            }
            if (this.listViewLocale.SelectedItems.Count == 1)
            {
                DocLocalization docLocal = (DocLocalization)this.listViewLocale.SelectedItems[0].Tag;
                this.textBoxGeneralName.Enabled = true;
                this.textBoxGeneralName.Text = docLocal.Name;
                this.textBoxGeneralDescription.Text = docLocal.Documentation;
                this.comboBoxLocaleCategory.SelectedIndex = (int)docLocal.Category;
                this.textBoxLocaleURL.Text = docLocal.URL;

                this.comboBoxLocaleCategory.Enabled = true;
                this.textBoxLocaleURL.Enabled = true;
            }
            else
            {
                this.textBoxGeneralName.Enabled = false;
                this.textBoxGeneralName.Text = this.m_target.Name;
                this.textBoxGeneralDescription.Text = this.m_target.Documentation;

                this.comboBoxLocaleCategory.Enabled = false;
                this.textBoxLocaleURL.Enabled = false;
                this.comboBoxLocaleCategory.SelectedIndex = -1;
                this.textBoxLocaleURL.Text = "";
            }

            this.buttonLocaleDelete.Enabled = (this.listViewLocale.SelectedItems.Count > 0);
        }

        private void textBoxGeneralName_TextChanged(object sender, EventArgs e)
        {
            if (this.listViewLocale.SelectedItems.Count > 0)
            {
                foreach (ListViewItem lvi in this.listViewLocale.SelectedItems)
                {
                    DocLocalization docLocal = (DocLocalization)lvi.Tag;
                    docLocal.Name = this.textBoxGeneralName.Text;

                    lvi.SubItems[1].Text = this.textBoxGeneralName.Text;
                }
            }
            else
            {
                //this.m_target.Name = this.textBoxGeneralName.Text;
                //this.Text = this.m_target.Name;
            }
        }

        private void textBoxGeneralDescription_TextChanged(object sender, EventArgs e)
        {
            if (this.listViewLocale.SelectedItems.Count > 0)
            {
                foreach (ListViewItem lvi in this.listViewLocale.SelectedItems)
                {
                    DocLocalization docLocal = (DocLocalization)lvi.Tag;
                    docLocal.Documentation = this.textBoxGeneralDescription.Text;

                    lvi.SubItems[2].Text = this.textBoxGeneralDescription.Text;
                }
            }
            else
            {
                this.m_target.Documentation = this.textBoxGeneralDescription.Text;
            }
        }

        private void buttonRuleAdd_Click(object sender, EventArgs e)
        {
            if (this.treeViewTemplateRules.SelectedNode != null &&
                this.treeViewTemplateRules.SelectedNode.Tag is DocModelRuleAttribute)
            {
                this.toolStripButtonInsertRuleEntity_Click(this, e);
            }
            else
            {
                this.toolStripButtonInsertRuleAttribute_Click(this, e);
            }

            // update diagram            
            this.panelDiagram.BackgroundImage = FormatPNG.CreateTemplateDiagram((DocTemplateDefinition)this.m_target, this.m_map, null, this.m_project);
        }

        private void buttonRuleDelete_Click(object sender, EventArgs e)
        {
            DocTemplateDefinition docTemplate = (DocTemplateDefinition)this.m_target;

            DocModelRule ruleTarget = this.treeViewTemplateRules.SelectedNode.Tag as DocModelRule;
            DocModelRule ruleParent = null;

            if (this.treeViewTemplateRules.SelectedNode.Parent != null)
            {
                ruleParent = this.treeViewTemplateRules.SelectedNode.Parent.Tag as DocModelRule;
            }

            if (ruleParent != null)
            {
                ruleParent.Rules.Remove(ruleTarget);
            }
            else
            {
                docTemplate.Rules.Remove(ruleTarget);
            }

            // copy to child templates (before clearing selection)
            docTemplate.PropagateRule(this.treeViewTemplateRules.SelectedNode.FullPath);

            ruleTarget.Delete();
            this.treeViewTemplateRules.SelectedNode.Remove();

            // update diagram            
            this.panelDiagram.BackgroundImage = FormatPNG.CreateTemplateDiagram((DocTemplateDefinition)this.m_target, this.m_map, null, this.m_project);
        }

        private void buttonLocaleAdd_Click(object sender, EventArgs e)
        {
            // launch form for picking locale...
            using (FormSelectLocale form = new FormSelectLocale())
            {
                DialogResult res = form.ShowDialog(this);
                if (res == DialogResult.OK && form.SelectedLocale != null)
                {
                    DocLocalization docLocal = new DocLocalization();
                    docLocal.Locale = form.SelectedLocale.Name;
                    this.m_target.Localization.Add(docLocal);

                    ListViewItem lvi = new ListViewItem();
                    lvi.Tag = docLocal;
                    lvi.Text = docLocal.Locale;
                    lvi.SubItems.Add("");
                    lvi.SubItems.Add("");
                    this.listViewLocale.Items.Add(lvi);

                    this.listViewLocale.SelectedItems.Clear();
                    lvi.Selected = true;
                }
            }
        }

        private void buttonLocaleDelete_Click(object sender, EventArgs e)
        {
            for(int i = this.listViewLocale.SelectedItems.Count-1; i >=0; i--)
            {
                ListViewItem lvi = this.listViewLocale.SelectedItems[i];
                DocLocalization docLocal = (DocLocalization)lvi.Tag;
                this.m_target.Localization.Remove(docLocal);
                docLocal.Delete();

                lvi.Remove();
            }
        }

        private void dataGridViewConceptRules_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            // eat it...
        }

        private void dataGridViewConceptRules_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;
            
            // for button types, launch dialog
            DataGridViewCell cell = this.dataGridViewConceptRules.Rows[e.RowIndex].Cells[e.ColumnIndex];
            DocDefinition docEntity = cell.Tag as DocDefinition;
            if(docEntity == null)
                return;

            DocDefinition docSelect = null;
            if (cell.Value != null && this.m_map.ContainsKey(cell.Value.ToString()))
            {
                docSelect = this.m_map[cell.Value.ToString()] as DocDefinition;
            }

            if (docEntity.Name != null && docEntity.Name.Equals("IfcReference"))
            {
                DocDefinition docDef = this.m_path[2] as DocDefinition;

                // special case for building reference paths
                using (FormReference form = new FormReference(this.m_project, docDef, this.m_map, cell.Value as string))
                {
                    DialogResult res = form.ShowDialog(this);
                    if (res == System.Windows.Forms.DialogResult.OK)
                    {
                        if (form.ValuePath != null && form.ValuePath.StartsWith("\\"))
                        {
                            cell.Value = form.ValuePath.Substring(1);
                        }
                        else if (form.ValuePath == "")
                        {
                            cell.Value = "\\";
                        }
                        this.dataGridViewConceptRules.NotifyCurrentCellDirty(true);
                    }
                }
            }
            else
            {
                using (FormSelectEntity form = new FormSelectEntity(docEntity, docSelect, this.m_project))
                {
                    DialogResult res = form.ShowDialog(this);
                    if (res == DialogResult.OK && form.SelectedEntity != null)
                    {
                        cell.Value = form.SelectedEntity.Name;
                        this.dataGridViewConceptRules.NotifyCurrentCellDirty(true);
                    }
                }
            }
        }

        private void buttonPsetEntity_Click(object sender, EventArgs e)
        {
            DocObject target = null;
            DocEntity entity = null;

            // get selected entity
            if (this.m_target is DocVariableSet)
            {
                DocVariableSet docTemplate = (DocVariableSet)this.m_target;
                if (docTemplate.ApplicableType != null && m_map.TryGetValue(docTemplate.ApplicableType, out target))
                {
                    entity = (DocEntity)target;
                }

                using (FormSelectEntity form = new FormSelectEntity(null, entity, this.m_project, true))
                {
                    DialogResult res = form.ShowDialog(this);
                    if (res == DialogResult.OK && form.SelectedEntity != null)
                    {
                        if (String.IsNullOrEmpty(docTemplate.ApplicableType))
                        {
                            docTemplate.ApplicableType = form.SelectedEntity.Name;
                        }
                        else
                        {
                            docTemplate.ApplicableType += "," + form.SelectedEntity.Name;
                        }

                        // append predefined type, if any
                        if (form.SelectedConstant != null)
                        {
                            docTemplate.ApplicableType += "/" + form.SelectedConstant.Name;
                        }

                        this.LoadApplicability();
                    }
                }
            }
        }

        private void comboBoxPsetType_SelectedIndexChanged(object sender, EventArgs e)
        {
            DocPropertySet docPset = (DocPropertySet)this.m_target;
            docPset.PropertySetType = this.comboBoxPsetType.Text;
        }

        private void comboBoxPsetType_TextUpdate(object sender, EventArgs e)
        {
            DocPropertySet docPset = (DocPropertySet)this.m_target;
            docPset.PropertySetType = this.comboBoxPsetType.Text;
        }

        private void comboBoxPropertyType_SelectedIndexChanged(object sender, EventArgs e)
        {
            DocProperty docProperty = (DocProperty)this.m_target;
            try
            {
                docProperty.PropertyType = (DocPropertyTemplateTypeEnum)Enum.Parse(typeof(DocPropertyTemplateTypeEnum), this.comboBoxPropertyType.SelectedItem.ToString());
            }
            catch
            {
                docProperty.PropertyType = DocPropertyTemplateTypeEnum.COMPLEX;
            }

            this.listViewPropertyEnums.Items.Clear();
            this.buttonPropertyEnumRemove.Enabled = false;
            switch (docProperty.PropertyType)
            {
                case DocPropertyTemplateTypeEnum.P_ENUMERATEDVALUE: // add/remove enum values
                    this.textBoxPropertyData.Text = "PEnum_";
                    this.textBoxPropertyData.ReadOnly = true;//false;
                    this.buttonPropertyData.Enabled = true;//false;
                    this.listViewPropertyEnums.Enabled = false;//true;
                    this.buttonPropertyEnumInsert.Enabled = false;//true;
                    //this.listViewPropertyEnums.Items.Add(new ListViewItem("OTHER"));
                    //this.listViewPropertyEnums.Items.Add(new ListViewItem("NOTKNOWN"));
                    //this.listViewPropertyEnums.Items.Add(new ListViewItem("UNSET"));
                    this.listViewPropertyEnums.LabelEdit = false;//true;
                    break;

                //case DocPropertyTemplateTypeEnum.P_TABLEVALUE: // add/remove column types? // or P_REFERENCEVALUE with IfcTable?
                //    break;

                case DocPropertyTemplateTypeEnum.P_REFERENCEVALUE:
                    this.textBoxPropertyData.Text = "IfcTimeSeries";
                    this.textBoxPropertyData.ReadOnly = true;
                    this.buttonPropertyData.Enabled = true;
                    this.listViewPropertyEnums.Enabled = true;
                    this.buttonPropertyEnumInsert.Enabled = true;
                    this.listViewPropertyEnums.Items.Add(new ListViewItem("IfcReal"));
                    this.listViewPropertyEnums.LabelEdit = false;
                    break;

                default:
                    this.textBoxPropertyData.Text = "IfcLabel";
                    this.textBoxPropertyData.ReadOnly = true;
                    this.buttonPropertyData.Enabled = true;
                    this.listViewPropertyEnums.Enabled = false;
                    this.buttonPropertyEnumInsert.Enabled = false;
                    break;
            }            
        }

        private void buttonPropertyData_Click(object sender, EventArgs e)
        {
            DocProperty docTemplate = (DocProperty)this.m_target;

            if(docTemplate.PropertyType == DocPropertyTemplateTypeEnum.P_ENUMERATEDVALUE)
            {
                // browse for property enumeration
                using(FormSelectPropertyEnum form = new FormSelectPropertyEnum(this.m_project, null))
                {
                    if (form.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
                    {
                        if (form.Selection != null)
                        {
                            docTemplate.PrimaryDataType = form.Selection.Name;
                        }
                        else
                        {
                            docTemplate.PrimaryDataType = String.Empty;
                        }
                        this.textBoxPropertyData.Text = docTemplate.PrimaryDataType;
                    }
                }
                return;
            }

            string basetypename = "IfcValue";
            switch (docTemplate.PropertyType)
            {
                case DocPropertyTemplateTypeEnum.P_REFERENCEVALUE:
                    basetypename = "IfcObjectReferenceSelect";
                    break;
            }            

            DocObject docobj = null;
            DocDefinition docEntity = null;
            if (this.m_map.TryGetValue(basetypename, out docobj))
            {
                docEntity = (DocDefinition)docobj;
            }

            // get selected entity
            DocObject target = null;
            DocDefinition entity = null;
            if (docTemplate.PrimaryDataType != null && m_map.TryGetValue(docTemplate.PrimaryDataType, out target))
            {
                entity = (DocDefinition)target;
            }

            using (FormSelectEntity form = new FormSelectEntity(docEntity, entity, this.m_project))
            {
                DialogResult res = form.ShowDialog(this);
                if (res == DialogResult.OK && form.SelectedEntity != null)
                {
                    docTemplate.PrimaryDataType = form.SelectedEntity.Name;
                    this.textBoxPropertyData.Text = docTemplate.PrimaryDataType;
                }
            }            
        }

        private void textBoxPropertyData_TextChanged(object sender, EventArgs e)
        {
            //DocProperty docTemplate = (DocProperty)this.m_target;
            //docTemplate.PrimaryDataType = this.textBoxPropertyData.Text;
        }

        private void buttonEntityBase_Click(object sender, EventArgs e)
        {
            DocEntity docEntity = (DocEntity)this.m_target;
            DocObject docBase = null;
            if (docEntity.BaseDefinition != null)
            {
                this.m_map.TryGetValue(docEntity.BaseDefinition, out docBase);
            }

            using (FormSelectEntity form = new FormSelectEntity(null, (DocEntity)docBase, this.m_project))
            {
                DialogResult res = form.ShowDialog(this);
                if (res == DialogResult.OK && form.SelectedEntity != null)
                {
                    docEntity.BaseDefinition = form.SelectedEntity.Name;
                    this.textBoxEntityBase.Text = docEntity.BaseDefinition;
                }
            }            
        }

        private void textBoxIdentityCode_TextChanged(object sender, EventArgs e)
        {
            this.m_target.Code = this.textBoxIdentityCode.Text;
        }

        private void textBoxIdentityVersion_TextChanged(object sender, EventArgs e)
        {
            this.m_target.Version = this.textBoxIdentityVersion.Text;
        }

        private void comboBoxIdentityStatus_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.m_target.Status = this.comboBoxIdentityStatus.Text;
        }

        private void comboBoxIdentityStatus_TextChanged(object sender, EventArgs e)
        {
            this.m_target.Status = this.comboBoxIdentityStatus.Text;
        }

        private void textBoxIdentityAuthor_TextChanged(object sender, EventArgs e)
        {
            this.m_target.Author = this.textBoxIdentityAuthor.Text;
        }

        private void textBoxIdentityOwner_TextChanged(object sender, EventArgs e)
        {
            this.m_target.Owner = this.textBoxIdentityOwner.Text;
        }

        private void textBoxIdentityCopyright_TextChanged(object sender, EventArgs e)
        {
            this.m_target.Copyright = this.textBoxIdentityCopyright.Text;
        }

        private void buttonRuleUpdate_Click(object sender, EventArgs e)
        {
            DocModelRule docRule = (DocModelRule)this.treeViewTemplateRules.SelectedNode.Tag;
            if (docRule is DocModelRuleConstraint)
            {
                using (FormConstraint form = new FormConstraint())
                {
                    form.Expression = docRule.Description;
                    DialogResult res = form.ShowDialog(this);
                    if (res == DialogResult.OK)
                    {
                        docRule.Description = form.Expression;
                        docRule.Name = form.Expression; // repeat for visibility
                    }
                }
            }
            else
            {
                using (FormRule form = new FormRule(docRule))
                {
                    form.ShowDialog(this);
                }
            }

            // update text in treeview
            this.UpdateTreeRule(this.treeViewTemplateRules.SelectedNode);

            // propagate rule
            DocTemplateDefinition docTemplate = (DocTemplateDefinition)this.m_target;
            docTemplate.PropagateRule(this.treeViewTemplateRules.SelectedNode.FullPath);

            /*
            this.treeViewTemplateRules.SelectedNode.Text = docRule.Name + docRule.GetCardinalityExpression();            
            if (!String.IsNullOrEmpty(docRule.Identification))
            {
                this.treeViewTemplateRules.SelectedNode.Text += " <" + docRule.Identification + ">";
            }*/
        }

        private void comboBoxLocaleCategory_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!this.comboBoxLocaleCategory.Enabled)
                return;

            if (String.IsNullOrEmpty(this.comboBoxLocaleCategory.Text))
                return;

            if (this.listViewLocale.SelectedItems.Count > 0)
            {
                foreach (ListViewItem lvi in this.listViewLocale.SelectedItems)
                {
                    DocLocalization docLocal = (DocLocalization)lvi.Tag;
                    docLocal.Category = (DocCategoryEnum)Enum.Parse(typeof(DocCategoryEnum), this.comboBoxLocaleCategory.Text);                    
                }
            }
        }

        private void textBoxLocaleURL_TextChanged(object sender, EventArgs e)
        {
            if (!this.textBoxLocaleURL.Enabled)
                return;

            if (this.listViewLocale.SelectedItems.Count > 0)
            {
                foreach (ListViewItem lvi in this.listViewLocale.SelectedItems)
                {
                    DocLocalization docLocal = (DocLocalization)lvi.Tag;
                    docLocal.URL = this.textBoxLocaleURL.Text;
                }
            }
        }

        private void MoveRule(int offset)
        {
            TreeNode tnSelect = this.treeViewTemplateRules.SelectedNode;
            TreeNode tnParent = tnSelect.Parent;
            DocModelRule ruleSelect = (DocModelRule)tnSelect.Tag;
            if (tnParent.Tag is DocModelRule)
            {
                DocModelRule ruleParent = (DocModelRule)tnParent.Tag;
                int index = ruleParent.Rules.IndexOf(ruleSelect);
                ruleParent.Rules.RemoveAt(index);

                index += offset;

                ruleParent.Rules.Insert(index, ruleSelect);
                tnSelect.Remove();
                tnParent.Nodes.Insert(index, tnSelect);
            }
            else if (tnParent.Tag is DocTemplateDefinition)
            {
                DocTemplateDefinition ruleParent = (DocTemplateDefinition)tnParent.Tag;
                int index = ruleParent.Rules.IndexOf(ruleSelect);
                ruleParent.Rules.RemoveAt(index);

                index += offset;

                ruleParent.Rules.Insert(index, ruleSelect);
                tnSelect.Remove();
                tnParent.Nodes.Insert(index, tnSelect);
            }

            this.treeViewTemplateRules.SelectedNode = tnSelect;

            // update diagram            
            this.panelDiagram.BackgroundImage = FormatPNG.CreateTemplateDiagram((DocTemplateDefinition)this.m_target, this.m_map, null, this.m_project);
        }

        private void buttonMoveUp_Click(object sender, EventArgs e)
        {
            MoveRule(-1);
        }

        private void buttonMoveDown_Click(object sender, EventArgs e)
        {
            MoveRule(+1);
        }

        private void buttonAttributeType_Click(object sender, EventArgs e)
        {
            using (FormSelectEntity form = new FormSelectEntity(null, null, this.m_project))
            {
                if (form.ShowDialog(this) == DialogResult.OK && form.SelectedEntity != null)
                {
                    DocAttribute docAttr = (DocAttribute)this.m_target;
                    docAttr.DefinedType = form.SelectedEntity.Name;
                    this.textBoxAttributeType.Text = docAttr.DefinedType;
                }
            }
        }

        private void checkBoxAttributeOptional_CheckedChanged(object sender, EventArgs e)
        {
            DocAttribute docAttr = (DocAttribute)this.m_target;
            if (this.checkBoxAttributeOptional.Checked)
            {
                docAttr.AttributeFlags |= 1;
            }
            else
            {
                docAttr.AttributeFlags &= ~1;
            }
        }

        private void checkBoxEntityAbstract_CheckedChanged(object sender, EventArgs e)
        {
            DocEntity docAttr = (DocEntity)this.m_target;
            if (this.checkBoxEntityAbstract.Checked)
            {
                docAttr.EntityFlags &= ~0x20;
            }
            else
            {
                docAttr.EntityFlags |= 0x20;
            }
        }

        private void textBoxExpression_TextChanged(object sender, EventArgs e)
        {
            DocConstraint docConstraint = (DocConstraint)this.m_target;
            docConstraint.Expression = this.textBoxExpression.Text;            
        }

        private void comboBoxQuantityType_SelectedIndexChanged(object sender, EventArgs e)
        {
            DocQuantity docProperty = (DocQuantity)this.m_target;
            try
            {
                docProperty.QuantityType = (DocQuantityTemplateTypeEnum)Enum.Parse(typeof(DocQuantityTemplateTypeEnum), this.comboBoxQuantityType.SelectedItem.ToString());
            }
            catch
            {
                docProperty.QuantityType = DocQuantityTemplateTypeEnum.Q_COUNT;
            }            
        }

        private void buttonViewBase_Click(object sender, EventArgs e)
        {
            using (FormSelectView form = new FormSelectView(this.m_project))
            {
                DialogResult res = form.ShowDialog();
                if (res == DialogResult.OK)
                {
                    DocModelView docView = (DocModelView)this.m_target;

                    if (form.Selection != null)
                    {
                        this.textBoxViewBase.Text = form.Selection.Name;
                        docView.BaseView = form.Selection.Uuid.ToString();
                    }
                    else
                    {
                        this.textBoxViewBase.Text = String.Empty;
                        docView.BaseView = null;
                    }
                }
            }
        }

        private void buttonPsetApplicabilityDelete_Click(object sender, EventArgs e)
        {
            List<DocTemplateDefinition> listTemplate = new List<DocTemplateDefinition>(); 

            // build new string
            StringBuilder sb = new StringBuilder();
            foreach (ListViewItem lvi in this.listViewPsetApplicability.Items)
            {
                if (!lvi.Selected)
                {
                    if (lvi.Tag is string)
                    {
                        if (sb.Length > 0)
                        {
                            sb.Append(",");
                        }

                        sb.Append(lvi.Tag as string);
                    }
                    else if (lvi.Tag is DocTemplateDefinition)
                    {
                        listTemplate.Add((DocTemplateDefinition)lvi.Tag); 
                    }
                }
            }

            DocVariableSet dvs = (DocVariableSet)this.m_target;
            if (sb.Length > 0)
            {
                dvs.ApplicableType = sb.ToString();
            }
            else
            {
                dvs.ApplicableType = null;
            }

            if (dvs is DocExample)
            {
                DocExample dex = (DocExample)dvs;
                dex.ApplicableTemplates = listTemplate;
            }

            this.LoadApplicability();
        }

        private void listViewPsetApplicability_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.buttonPsetApplicabilityDelete.Enabled = (this.listViewPsetApplicability.SelectedItems.Count > 0);
        }

        private void buttonPropertyEnumInsert_Click(object sender, EventArgs e)
        {
            if (this.comboBoxPropertyType.SelectedIndex == 5)
            {
                // reference value -- pick type of column for table or time series
                DocObject docBase = null;
                if (this.m_map.TryGetValue("IfcValue", out docBase))
                {
                    using (FormSelectEntity form = new FormSelectEntity(docBase as DocDefinition, null, this.m_project))
                    {
                        if (form.ShowDialog(this) == DialogResult.OK && form.SelectedEntity != null)
                        {
                            ListViewItem lvi = new ListViewItem();
                            lvi.Tag = form.SelectedEntity;
                            lvi.Text = form.SelectedEntity.Name;
                            this.listViewPropertyEnums.Items.Add(lvi);
                            this.SavePropertyEnums();
                        }
                    }
                }
            }
            else
            {
                ListViewItem lvi = new ListViewItem();
                this.listViewPropertyEnums.Items.Add(lvi);
                lvi.BeginEdit();
            }
        }

        private void buttonPropertyEnumRemove_Click(object sender, EventArgs e)
        {
            for (int i = this.listViewPropertyEnums.SelectedItems.Count - 1; i >= 0; i--)
            {
                this.listViewPropertyEnums.SelectedItems[i].Remove();
            }

            this.SavePropertyEnums();
        }

        private void listViewPropertyEnums_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.buttonPropertyEnumRemove.Enabled = (this.listViewPropertyEnums.SelectedItems.Count > 0);
        }

        private void listViewPropertyEnums_AfterLabelEdit(object sender, LabelEditEventArgs e)
        {
            if (e.CancelEdit)
            {
                //this.listViewPropertyEnums.Items[e.Item].Tag;
                //this.listViewPropertyEnums.Items[e.Item].Text = e.Label;
                //...
                return;
            }

            this.listViewPropertyEnums.Items[e.Item].Text = e.Label;
            this.SavePropertyEnums();
        }

        private void SavePropertyEnums()
        {
            // save enums or table columns

            DocProperty docProp = (DocProperty)this.m_target;
            if (docProp.PropertyType == DocPropertyTemplateTypeEnum.P_ENUMERATEDVALUE)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(this.textBoxPropertyData.Text);
                sb.Append(":");
                foreach (ListViewItem lvi in this.listViewPropertyEnums.Items)
                {
                    if (sb[sb.Length - 1] != ':')
                    {
                        sb.Append(",");
                    }

                    sb.Append(lvi.Text);
                }

                docProp.SecondaryDataType = sb.ToString();
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                foreach (ListViewItem lvi in this.listViewPropertyEnums.Items)
                {
                    if (sb.Length != 0)
                    {
                        sb.Append(",");
                    }

                    sb.Append(lvi.Text);
                }

                docProp.SecondaryDataType = sb.ToString();
            }
        }

        private void buttonConceptMoveUp_Click(object sender, EventArgs e)
        {
            this.m_editcon = true;
            int index = this.dataGridViewConceptRules.SelectedRows[0].Index;
            DocTemplateUsage docUsage = (DocTemplateUsage)this.m_target;
            DocTemplateItem dti = docUsage.Items[index];
            docUsage.Items.Insert(index, dti);
            docUsage.Items.RemoveAt(index + 1);

            LoadUsage();
            this.dataGridViewConceptRules.Rows[index - 1].Selected = true;
            this.m_editcon = false;
        }

        private void buttonConceptMoveDown_Click(object sender, EventArgs e)
        {
            this.m_editcon = true;
            int index = this.dataGridViewConceptRules.SelectedRows[0].Index;
            if (index < this.dataGridViewConceptRules.Rows.Count - 2)
            {
                DocTemplateUsage docUsage = (DocTemplateUsage)this.m_target;
                DocTemplateItem dti = docUsage.Items[index];
                docUsage.Items.Insert(index + 2, dti);
                docUsage.Items.RemoveAt(index);

                LoadUsage();
                this.dataGridViewConceptRules.Rows[index + 1].Selected = true;
            }
            this.m_editcon = false;
        }

        private void buttonConceptDelete_Click(object sender, EventArgs e)
        {
            this.m_editcon = true;
            int index = this.dataGridViewConceptRules.SelectedRows[0].Index;
            DocTemplateUsage docUsage = (DocTemplateUsage)this.m_target;
            docUsage.Items.RemoveAt(index);

            LoadUsage();

            if (this.dataGridViewConceptRules.Rows.Count > index)
            {
                this.dataGridViewConceptRules.Rows[index].Selected = true;
            }
            this.m_editcon = false;
        }

        private void dataGridViewConceptRules_SelectionChanged(object sender, EventArgs e)
        {
            this.buttonConceptDelete.Enabled = (this.dataGridViewConceptRules.SelectedRows.Count == 1 && this.dataGridViewConceptRules.SelectedRows[0].Index < this.dataGridViewConceptRules.Rows.Count - 1);
            this.buttonConceptMoveDown.Enabled = (this.dataGridViewConceptRules.SelectedRows.Count == 1 && this.dataGridViewConceptRules.SelectedRows[0].Index < this.dataGridViewConceptRules.Rows.Count - 2); // exclude New row
            this.buttonConceptMoveUp.Enabled = (this.dataGridViewConceptRules.SelectedRows.Count == 1 && this.dataGridViewConceptRules.SelectedRows[0].Index > 0 && this.dataGridViewConceptRules.SelectedRows[0].Index < this.dataGridViewConceptRules.Rows.Count - 1);
        }

        private void checkBoxExchangeImport_CheckedChanged(object sender, EventArgs e)
        {
            DocExchangeDefinition docExchange = (DocExchangeDefinition)this.m_target;
            if (this.checkBoxExchangeImport.Checked)
            {
                docExchange.Applicability |= DocExchangeApplicabilityEnum.Import;
            }
            else
            {
                docExchange.Applicability &= ~DocExchangeApplicabilityEnum.Import;
            }
        }

        private void checkBoxExchangeExport_CheckedChanged(object sender, EventArgs e)
        {
            DocExchangeDefinition docExchange = (DocExchangeDefinition)this.m_target;
            if (this.checkBoxExchangeImport.Checked)
            {
                docExchange.Applicability |= DocExchangeApplicabilityEnum.Export;
            }
            else
            {
                docExchange.Applicability &= ~DocExchangeApplicabilityEnum.Export;
            }
        }

        private void buttonExchangeIconChange_Click(object sender, EventArgs e)
        {
            DialogResult res = this.openFileDialogIcon.ShowDialog();
            if (res != System.Windows.Forms.DialogResult.OK)
                return;

            DocExchangeDefinition docExchange = (DocExchangeDefinition)this.m_target;

            try
            {
                using (System.IO.FileStream filestream = new System.IO.FileStream(this.openFileDialogIcon.FileName, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {
                    docExchange.Icon = new byte[filestream.Length];
                    filestream.Read(docExchange.Icon, 0, docExchange.Icon.Length);
                }

                this.panelIcon.BackgroundImage = Image.FromStream(new System.IO.MemoryStream(docExchange.Icon));
            }
            catch(Exception x)
            {
                MessageBox.Show(this, x.Message, "Error", MessageBoxButtons.OK); 
            }
        }

        private void buttonExchangeIconClear_Click(object sender, EventArgs e)
        {
            DocExchangeDefinition docExchange = (DocExchangeDefinition)this.m_target;

            docExchange.Icon = null;
            this.panelIcon.BackgroundImage = null;
        }

        private void buttonApplicabilityAddTemplate_Click(object sender, EventArgs e)
        {
            using (FormSelectTemplate form = new FormSelectTemplate(null, this.m_project, null))
            {
                if (form.ShowDialog(this) == System.Windows.Forms.DialogResult.OK && form.SelectedTemplate != null)
                {
                    DocExample docExample = (DocExample)this.m_target;
                    if (docExample.ApplicableTemplates == null)
                    {
                        docExample.ApplicableTemplates = new List<DocTemplateDefinition>();
                    }

                    docExample.ApplicableTemplates.Add(form.SelectedTemplate);

                    this.LoadApplicability();
                }
            }
        }

        private void comboBoxAttributeXsdFormat_SelectedIndexChanged(object sender, EventArgs e)
        {
            DocAttribute docAttr = (DocAttribute)this.m_target;
            if (this.comboBoxAttributeXsdFormat.SelectedItem != null)
            {
                DocXsdFormatEnum formatnew = (DocXsdFormatEnum)Enum.Parse(typeof(DocXsdFormatEnum), this.comboBoxAttributeXsdFormat.SelectedItem as string, true);
                if (docAttr.XsdFormat != formatnew)
                {
                    docAttr.XsdFormat = formatnew;
                    //... set modified
                }
            }
        }

        private void checkBoxXsdTagless_CheckedChanged(object sender, EventArgs e)
        {
            DocAttribute docAttr = (DocAttribute)this.m_target;
            docAttr.XsdTagless = this.checkBoxXsdTagless.Checked;
        }

        private void buttonUsageEdit_Click(object sender, EventArgs e)
        {
            DocObject[] path = (DocObject[])this.listViewUsage.SelectedItems[0].Tag;
            using (FormProperties form = new FormProperties(path, this.m_project))
            {
                form.ShowDialog(this);
            }
        }

        private void buttonUsageMigrate_Click(object sender, EventArgs e)
        {
            DocTemplateDefinition docTemplate = (DocTemplateDefinition)this.m_target;
            DocEntity docEntity = (DocEntity)this.m_map[docTemplate.Type];

            using (FormSelectTemplate form = new FormSelectTemplate(docTemplate, this.m_project, null))
            {
                DialogResult res = form.ShowDialog(this);
                if (res == System.Windows.Forms.DialogResult.OK && form.SelectedTemplate != null && form.SelectedTemplate != docTemplate)
                {
                    while (this.listViewUsage.SelectedItems.Count > 0)
                    {
                        ListViewItem lvi = this.listViewUsage.SelectedItems[0];
                        DocObject[] path = (DocObject[])lvi.Tag;
                        DocTemplateUsage usage = (DocTemplateUsage)path[2];
                        usage.Definition = form.SelectedTemplate;

                        lvi.Remove();
                    }
                }
            }
        }

        private void listViewUsage_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.buttonUsageEdit.Enabled = (this.listViewUsage.SelectedItems.Count == 1);
            this.buttonUsageMigrate.Enabled = (this.listViewUsage.SelectedItems.Count > 0);
        }

        private void comboBoxExchangeClassProcess_Validated(object sender, EventArgs e)
        {
            DocExchangeDefinition docExchange = (DocExchangeDefinition)this.m_target;
            docExchange.ExchangeClass = this.comboBoxExchangeClassProcess.Text;
        }

        private void comboBoxExchangeClassSender_Validated(object sender, EventArgs e)
        {
            DocExchangeDefinition docExchange = (DocExchangeDefinition)this.m_target;
            docExchange.SenderClass = this.comboBoxExchangeClassSender.Text;
        }

        private void comboBoxExchangeClassReceiver_Validated(object sender, EventArgs e)
        {
            DocExchangeDefinition docExchange = (DocExchangeDefinition)this.m_target;
            docExchange.ReceiverClass = this.comboBoxExchangeClassReceiver.Text;
        }

        private void buttonApplicabilityView_Click(object sender, EventArgs e)
        {
            DocExample docExample = this.m_target as DocExample;
            if (docExample == null)
                return;

            using (FormSelectView form = new FormSelectView(this.m_project))
            {
                form.Selection = docExample.ModelView;
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    docExample.ModelView = form.Selection;
                }
            }
        }

        private void checkBoxConceptOverride_CheckedChanged(object sender, EventArgs e)
        {
            DocTemplateUsage docUsage = (DocTemplateUsage)this.m_target;
            docUsage.Override = this.checkBoxConceptOverride.Checked;
        }

        private void buttonAttributeInverse_Click(object sender, EventArgs e)
        {
            DocAttribute docAttr = (DocAttribute)this.m_target;
            DocObject docEntity = null;
            if (this.m_map.TryGetValue(docAttr.DefinedType, out docEntity) && docEntity is DocEntity)
            {
                using(FormSelectAttribute form = new FormSelectAttribute((DocEntity)docEntity, this.m_map, null, false))
                {
                    if(form.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
                    {
                        if (form.SelectedAttribute != null)
                        {
                            docAttr.Inverse = form.SelectedAttribute.Name;
                            this.textBoxAttributeInverse.Text = docAttr.Inverse;
                        }
                        else
                        {
                            docAttr.Inverse = null;
                            this.textBoxAttributeInverse.Text = String.Empty;
                        }
                    }
                }
            }
        }

        private void listViewAttributeCardinality_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.buttonAttributeAggregationRemove.Enabled = (this.listViewAttributeCardinality.Items.Count > 1);

            DocAttribute docAttr = this.GetAttributeAggregation();

            this.m_loadagg = true;
            this.comboBoxAttributeAggregation.SelectedIndex = docAttr.AggregationType;
            this.textBoxAttributeAggregationMin.Text = docAttr.AggregationLower;
            this.textBoxAttributeAggregationMax.Text = docAttr.AggregationUpper;
            this.m_loadagg = false;
        }

        private DocAttribute GetAttributeAggregation()
        {
            DocAttribute docAttr = (DocAttribute)this.m_target;
            if (this.listViewAttributeCardinality.SelectedItems.Count == 1)
            {
                docAttr = (DocAttribute)this.listViewAttributeCardinality.SelectedItems[0].Tag;
            }
            return docAttr;
        }

        private void LoadAttributeCardinality()
        {
            this.m_loadagg = true;
            DocAttribute docAttr = (DocAttribute)this.m_target;
            this.listViewAttributeCardinality.Items.Clear();
            while(docAttr != null)
            {
                ListViewItem lvi = new ListViewItem();
                lvi.Tag = docAttr;
                lvi.Text = docAttr.GetAggregation().ToString();
                lvi.SubItems.Add(docAttr.AggregationLower);
                lvi.SubItems.Add(docAttr.AggregationUpper);
                this.listViewAttributeCardinality.Items.Add(lvi);

                docAttr = docAttr.AggregationAttribute;
            }
            this.m_loadagg = false;
        }

        private void buttonAttributeAggregationInsert_Click(object sender, EventArgs e)
        {
            DocAttribute docAttr = (DocAttribute)this.m_target;
            while(docAttr.AggregationAttribute != null)
            {
                docAttr = docAttr.AggregationAttribute;
            }

            docAttr.AggregationAttribute = new DocAttribute();
            this.LoadAttributeCardinality();
        }

        private void buttonAttributeAggregationRemove_Click(object sender, EventArgs e)
        {
            DocAttribute docAttr = (DocAttribute)this.m_target;
            while (docAttr.AggregationAttribute != null && docAttr.AggregationAttribute.AggregationAttribute != null)
            {
                docAttr = docAttr.AggregationAttribute;
            }

            docAttr.AggregationAttribute.Delete();
            docAttr.AggregationAttribute = null;

            this.LoadAttributeCardinality();
        }

        private void comboBoxAttributeAggregation_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.m_loadagg)
                return;

            DocAttribute docAttr = this.GetAttributeAggregation();
            docAttr.AggregationType = this.comboBoxAttributeAggregation.SelectedIndex;
            this.LoadAttributeCardinality();
        }

        private void textBoxAttributeAggregationMin_TextChanged(object sender, EventArgs e)
        {
            if (this.m_loadagg)
                return;

            DocAttribute docAttr = this.GetAttributeAggregation();
            docAttr.AggregationLower = this.textBoxAttributeAggregationMin.Text;
            this.LoadAttributeCardinality();
        }

        private void textBoxAttributeAggregationMax_TextChanged(object sender, EventArgs e)
        {
            if (this.m_loadagg)
                return;

            DocAttribute docAttr = this.GetAttributeAggregation();
            docAttr.AggregationUpper = this.textBoxAttributeAggregationMax.Text;
            this.LoadAttributeCardinality();
        }

 
        private void buttonViewXsdAttribute_Click(object sender, EventArgs e)
        {
            using (FormSelectEntity form = new FormSelectEntity(null, null, this.m_project, true))
            {
                if (form.ShowDialog(this) == System.Windows.Forms.DialogResult.OK && form.SelectedEntity is DocEntity)
                {
                    using(FormSelectAttribute formAttr = new FormSelectAttribute((DocEntity)form.SelectedEntity, this.m_map, null, false))
                    {
                        if(formAttr.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
                        {
                            DocXsdFormat docFormat = new DocXsdFormat();
                            docFormat.Entity = form.SelectedEntity.Name;
                            docFormat.Attribute = formAttr.SelectedAttribute.Name;

                            DocModelView docView = (DocModelView)this.m_target;
                            docView.XsdFormats.Add(docFormat);

                            ListViewItem lvi = new ListViewItem();
                            lvi.Tag = docFormat;
                            lvi.Text = docFormat.Entity;
                            lvi.SubItems.Add(docFormat.Attribute);
                            lvi.SubItems.Add(docFormat.XsdFormat.ToString());
                            lvi.SubItems.Add(docFormat.XsdTagless.ToString());

                            this.listViewViewXsd.Items.Add(lvi);

                            lvi.Selected = true;
                        }
                    }
                }
            }

        }

        private void buttonViewXsdDelete_Click(object sender, EventArgs e)
        {
            DocXsdFormat docFormat = (DocXsdFormat)this.listViewViewXsd.SelectedItems[0].Tag;
            DocModelView docView = (DocModelView)this.m_target;
            docView.XsdFormats.Remove(docFormat);
            docFormat.Delete();

            this.listViewViewXsd.Items.RemoveAt(this.listViewViewXsd.SelectedIndices[0]);
        }

        private void comboBoxViewXsd_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.listViewViewXsd.SelectedItems.Count != 1)
                return;

            DocXsdFormat docFormat = (DocXsdFormat)this.listViewViewXsd.SelectedItems[0].Tag;
            docFormat.XsdFormat = (DocXsdFormatEnum)this.comboBoxViewXsd.SelectedIndex;
            this.listViewViewXsd.SelectedItems[0].SubItems[2].Text = docFormat.XsdFormat.ToString();
        }

        private void checkBoxViewXsdTagless_CheckedChanged(object sender, EventArgs e)
        {
            DocXsdFormat docFormat = (DocXsdFormat)this.listViewViewXsd.SelectedItems[0].Tag;
            docFormat.XsdTagless = this.checkBoxViewXsdTagless.Checked;
            this.listViewViewXsd.SelectedItems[0].SubItems[3].Text = docFormat.XsdTagless.ToString();
        }

        private void listViewViewXsd_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.buttonViewXsdDelete.Enabled = (this.listViewViewXsd.SelectedItems.Count == 1);

            if (this.listViewViewXsd.SelectedItems.Count != 1)
            {
                this.comboBoxViewXsd.Enabled = false;
                this.comboBoxViewXsd.SelectedIndex = 0;
                this.checkBoxXsdTagless.Enabled = false;
                this.checkBoxXsdTagless.Checked = false;
                return;
            }

            this.comboBoxViewXsd.Enabled = true;
            this.checkBoxXsdTagless.Enabled = true;

            DocXsdFormat docFormat = (DocXsdFormat)this.listViewViewXsd.SelectedItems[0].Tag;
            this.comboBoxViewXsd.SelectedIndex = (int)docFormat.XsdFormat;
            this.checkBoxViewXsdTagless.Checked = docFormat.XsdTagless;
        }


    }    
}
