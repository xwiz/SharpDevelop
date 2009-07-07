﻿// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Martin Koníček" email="martin.konicek@gmail.com"/>
//     <version>$Revision$</version>
// </file>
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using Debugger.AddIn.Visualizers.Graph.Layout;
using ICSharpCode.SharpDevelop.Debugging;
using ICSharpCode.SharpDevelop.Services;

namespace Debugger.AddIn.Visualizers.Graph
{
	/// <summary>
	/// Interaction logic for VisualizerWPFWindow.xaml
	/// </summary>
	public partial class VisualizerWPFWindow : Window
	{
		private WindowsDebugger debuggerService;
		private EnumViewModel<LayoutDirection> layoutViewModel;
		private ObjectGraph objectGraph;
		private ObjectGraphBuilder objectGraphBuilder;
		
		private PositionedGraph oldPosGraph;
		private PositionedGraph currentPosGraph;
		private GraphDrawer graphDrawer;
		
		/// <summary>
		/// Long-lived map telling which graph nodes the user expanded.
		/// </summary>
		private ExpandedExpressions expandedGraphNodes;
		/// <summary>
		/// Long-lived map telling which content nodes inside graph nodes the user has expanded.
		/// </summary>
		private ExpandedContentNodes expandedContentNodes;

		public VisualizerWPFWindow()
		{
			InitializeComponent();
			
			debuggerService = DebuggerService.CurrentDebugger as WindowsDebugger;
			if (debuggerService == null)
				throw new ApplicationException("Only windows debugger is currently supported");
			
			debuggerService.IsProcessRunningChanged += new EventHandler(debuggerService_IsProcessRunningChanged);
			debuggerService.DebugStopped += new EventHandler(_debuggerService_DebugStopped);
			
			this.layoutViewModel = new EnumViewModel<LayoutDirection>();
			this.layoutViewModel.PropertyChanged += new PropertyChangedEventHandler(layoutViewModel_PropertyChanged);
			this.DataContext = this.layoutViewModel;
			
			this.graphDrawer = new GraphDrawer(this.canvas);
			
			this.expandedGraphNodes = new ExpandedExpressions();
		}
		
		public void debuggerService_IsProcessRunningChanged(object sender, EventArgs e)
		{
			// on step or breakpoint hit
			if (!debuggerService.IsProcessRunning)
			{
				refreshGraph();
			}
		}
		
		public void _debuggerService_DebugStopped(object sender, EventArgs e)
		{
			debuggerService.IsProcessRunningChanged -= new EventHandler(debuggerService_IsProcessRunningChanged);
			this.Close();
		}
		
		private void Inspect_Button_Click(object sender, RoutedEventArgs e)
		{
			refreshGraph();
		}
		
		void layoutViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "SelectedEnumValue")	// TODO should be special event for enum value change
			{
				layoutGraph(this.objectGraph);
			}
		}
		
		void refreshGraph()
		{
			bool graphBuiltOk = true;
			guiClearException();
			try
			{
				this.objectGraph = rebuildGraph();
			}
			catch(DebuggerVisualizerException ex)
			{
				graphBuiltOk = false;
				guiHandleException(ex);
			}
			catch(Debugger.GetValueException ex)
			{
				graphBuiltOk = false;
				guiHandleException(ex);
			}
			if (graphBuiltOk)
			{
				layoutGraph(this.objectGraph);
			}
		}
		
		ObjectGraph rebuildGraph()
		{
			this.objectGraphBuilder = new ObjectGraphBuilder(debuggerService);
			ICSharpCode.Core.LoggingService.Debug("Debugger visualizer: Building graph for expression: " + txtExpression.Text);
			return this.objectGraphBuilder.BuildGraphForExpression(txtExpression.Text, this.expandedGraphNodes);
		}
		
		void layoutGraph(ObjectGraph graph)
		{
			Layout.TreeLayouter layouter = new Layout.TreeLayouter();

			this.oldPosGraph = this.currentPosGraph;
			
			ICSharpCode.Core.LoggingService.Debug("Debugger visualizer: Calculating graph layout");
			this.currentPosGraph = layouter.CalculateLayout(graph, layoutViewModel.SelectedEnumValue, this.expandedGraphNodes);
			
			registerExpandCollapseEvents(this.currentPosGraph);
			
			var graphDiff = new GraphMatcher().MatchGraphs(oldPosGraph, currentPosGraph);
			this.graphDrawer.StartAnimation(oldPosGraph, currentPosGraph, graphDiff);
			//this.graphDrawer.Draw(this.currentPosGraph);
		}
		
		void guiClearException()
		{
			this.pnlError.Visibility = Visibility.Collapsed;
		}
		
		void guiHandleException(System.Exception ex)
		{
			this.txtError.Text = ex.Message;
			this.pnlError.Visibility = Visibility.Visible;
			//MessageBox.Show(ex.Message, "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
		}
		
		void registerExpandCollapseEvents(PositionedGraph posGraph)
		{
			foreach (var node in posGraph.Nodes)
			{
				node.PropertyExpanded += new EventHandler<PositionedPropertyEventArgs>(node_PropertyExpanded);
				node.PropertyCollapsed += new EventHandler<PositionedPropertyEventArgs>(node_PropertyCollapsed);
				node.ContentNodeExpanded += new EventHandler<NestedNodeViewModelEventArgs>(node_ContentNodeExpanded);
				node.ContentNodeCollapsed += new EventHandler<NestedNodeViewModelEventArgs>(node_ContentNodeCollapsed);
			}
		}
		
		void node_ContentNodeExpanded(object sender, NestedNodeViewModelEventArgs e)
		{
			expandedContentNodes.SetExpanded(e.Node);
			layoutGraph(this.objectGraph);
		}

		void node_ContentNodeCollapsed(object sender, NestedNodeViewModelEventArgs e)
		{
			expandedContentNodes.SetCollapsed(e.Node);
			layoutGraph(this.objectGraph);
		}

		void node_PropertyExpanded(object sender, PositionedPropertyEventArgs e)
		{
			// remember this property is expanded (for later graph rebuilds)
			expandedGraphNodes.SetExpanded(e.Property.Expression);
			
			// add edge (+ possibly nodes) to underlying object graph (no need to fully rebuild)
			// TODO can add more nodes if they are expanded - now this adds always one node
			e.Property.ObjectGraphProperty.TargetNode = this.objectGraphBuilder.ObtainNodeForExpression(e.Property.Expression);
			layoutGraph(this.objectGraph);
		}
		
		void node_PropertyCollapsed(object sender, PositionedPropertyEventArgs e)
		{
			// remember this property is collapsed (for later graph rebuilds)
			expandedGraphNodes.SetCollapsed(e.Property.Expression);
			
			// just remove edge from underlying object graph (no need to fully rebuild)
			e.Property.ObjectGraphProperty.TargetNode = null;
			layoutGraph(this.objectGraph);
		}
	}
}