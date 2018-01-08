// This file was automatically generated from IFCDOC at www.buildingsmart-tech.org.
// IFC content is copyright (C) 1996-2018 BuildingSMART International Ltd.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Xml.Serialization;

using BuildingSmart.IFC.IfcMeasureResource;

namespace BuildingSmart.IFC.IfcStructuralLoadResource
{
	[Guid("73b41ca6-4101-4683-b783-ffbc11578bb0")]
	public partial class IfcFailureConnectionCondition : IfcStructuralConnectionCondition
	{
		[DataMember(Order=0)] 
		[XmlAttribute]
		IfcForceMeasure? _TensionFailureX;
	
		[DataMember(Order=1)] 
		[XmlAttribute]
		IfcForceMeasure? _TensionFailureY;
	
		[DataMember(Order=2)] 
		[XmlAttribute]
		IfcForceMeasure? _TensionFailureZ;
	
		[DataMember(Order=3)] 
		[XmlAttribute]
		IfcForceMeasure? _CompressionFailureX;
	
		[DataMember(Order=4)] 
		[XmlAttribute]
		IfcForceMeasure? _CompressionFailureY;
	
		[DataMember(Order=5)] 
		[XmlAttribute]
		IfcForceMeasure? _CompressionFailureZ;
	
	
		[Description("Tension force in x-direction leading to failure of the connection.")]
		public IfcForceMeasure? TensionFailureX { get { return this._TensionFailureX; } set { this._TensionFailureX = value;} }
	
		[Description("Tension force in y-direction leading to failure of the connection. ")]
		public IfcForceMeasure? TensionFailureY { get { return this._TensionFailureY; } set { this._TensionFailureY = value;} }
	
		[Description("Tension force in z-direction leading to failure of the connection.")]
		public IfcForceMeasure? TensionFailureZ { get { return this._TensionFailureZ; } set { this._TensionFailureZ = value;} }
	
		[Description("Compression force in x-direction leading to failure of the connection. ")]
		public IfcForceMeasure? CompressionFailureX { get { return this._CompressionFailureX; } set { this._CompressionFailureX = value;} }
	
		[Description("Compression force in y-direction leading to failure of the connection.")]
		public IfcForceMeasure? CompressionFailureY { get { return this._CompressionFailureY; } set { this._CompressionFailureY = value;} }
	
		[Description("Compression force in z-direction leading to failure of the connection.")]
		public IfcForceMeasure? CompressionFailureZ { get { return this._CompressionFailureZ; } set { this._CompressionFailureZ = value;} }
	
	
	}
	
}