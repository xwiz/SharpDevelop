﻿// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Matthew Ward" email="mrward@users.sourceforge.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using ICSharpCode.SharpDevelop.Dom;
using NUnit.Framework;
using XmlEditor.Tests.Utils;

namespace XmlEditor.Tests.Folding
{
	[TestFixture]
	public class TwoElementFoldsTestFixture
	{
		XmlFoldParserHelper helper;
		
		[SetUp]
		public void Init()
		{
			string xml = 
				"<root>\r\n" +
				"    <child>\r\n" +
				"    </child>\r\n" +
				"</root>";
			
			helper = new XmlFoldParserHelper();
			helper.CreateParser();
			helper.GetFolds(xml);
		}
		
		[Test]
		public void GetFolds_TwoElements_FirstFoldRegionCoversRootElement()
		{
			DomRegion region = helper.GetFirstFoldRegion();
			
			int beginLine = 1;
			int endLine = 4;
			int beginCol = 1;
			int endCol = 8;
			DomRegion expectedRegion = new DomRegion(beginLine, beginCol, endLine, endCol);
			
			Assert.AreEqual(expectedRegion, region);
		}
		
		[Test]
		public void GetFolds_TwoElements_SecondFoldRegionCoversChildElement()
		{
			DomRegion region = helper.GetSecondFoldRegion();
			
			int beginLine = 2;
			int endLine = 3;
			int beginCol = 5;
			int endCol = 13;
			DomRegion expectedRegion = new DomRegion(beginLine, beginCol, endLine, endCol);
			
			Assert.AreEqual(expectedRegion, region);
		}
	}
}