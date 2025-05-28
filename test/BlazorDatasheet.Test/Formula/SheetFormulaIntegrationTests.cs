using System.Collections.Generic;
using System.Linq;
using BlazorDatasheet.Core.Commands.Data;
using BlazorDatasheet.Core.Data;
using BlazorDatasheet.DataStructures.Geometry;
using BlazorDatasheet.Formula.Core;
using BlazorDatasheet.Formula.Core.Interpreter;
using BlazorDatasheet.Formula.Core.Interpreter.Evaluation;
using BlazorDatasheet.Formula.Core.Interpreter.Parsing;
using BlazorDatasheet.Formula.Core.Interpreter.References;
using BlazorDatasheet.Formula.Functions.Logical;
using FluentAssertions;
using NUnit.Framework;

namespace BlazorDatasheet.Test.Formula;

public class SheetFormulaIntegrationTests
{
    private Sheet _sheet;

    [SetUp]
    public void TestSetup()
    {
        _sheet = new Sheet(50, 10);
    }

    [Test]
    public void Accept_Edit_With_Formula_String_Sets_Formula()
    {
        _sheet.Cells.SetValue(0, 0, 5);
        _sheet.Editor.BeginEdit(1, 1);
        _sheet.Editor.EditValue = "=A1 + 10";
        _sheet.Editor.AcceptEdit();

        Assert.IsTrue(_sheet.Cells.HasFormula(1, 1));
        _sheet.Cells.SetValue(0, 0, 5);
        var formulaVal = _sheet.Cells.GetValue(1, 1);
        Assert.AreEqual(15, formulaVal);
    }

    [Test]
    public void Set_Formula_Then_Undo_Removes_Formula()
    {
        _sheet.Cells.SetFormula(0, 0, "=5");
        _sheet.Commands.Undo();
        _sheet.Cells.GetValue(0, 0).Should().BeNull();
    }

    [Test]
    public void Formula_Calculation_Performs_When_Referenced_Cell_Value_Changes()
    {
        _sheet.Cells.SetFormula(1, 1, "=A1 + 10");
        _sheet.Cells.SetValue(0, 0, 5);
        Assert.AreEqual(15, _sheet.Cells.GetValue(1, 1));
    }

    [Test]
    public void Formula_Calculation_Performs_When_Formula_Is_Set()
    {
        _sheet.Cells.SetValue(0, 0, 5);
        _sheet.Cells.SetFormula(1, 1, "=A1 + 10");
        Assert.AreEqual(15, _sheet.Cells.GetValue(1, 1));
    }

    [Test]
    public void Formula_Calculation_Performs_When_Formula_Is_Set_Over_Formula()
    {
        _sheet.Cells.SetValue(0, 0, 2);
        _sheet.Cells.SetFormula(1, 0, "=A1");
        _sheet.Cells.SetFormula(2, 0, "=A2");
        _sheet.Cells.SetFormula(1, 0, "=A1");
        _sheet.Cells[2, 0].Value.Should().Be(2);
    }

    [Test]
    public void Setting_Cell_Value_Will_Clear_Formula()
    {
        _sheet.Cells.SetFormula(1, 1, "=A1");
        Assert.IsTrue(_sheet.Cells.HasFormula(1, 1));

        // Set sheet cell (1, 1) to any old value and the formula should be cleared.
        _sheet.Cells.SetValue(1, 1, "Blah");
        Assert.IsFalse(_sheet.Cells.HasFormula(1, 1));
        _sheet.FormulaEngine.DependencyManager.HasDependents(0, 0, _sheet.Name).Should().BeFalse();
    }

    [Test]
    public void Clear_Sheet_Cell_Will_Clear_Formula()
    {
        _sheet.Cells.SetFormula(1, 1, "=A1");
        Assert.IsTrue(_sheet.Cells.HasFormula(1, 1));
        _sheet.Cells.ClearCells(new Region(1, 1));
        Assert.IsFalse(_sheet.Cells.HasFormula(1, 1));
    }

    [Test]
    public void Setting_Invalid_Formula_Will_Not_Set_Formula()
    {
        var invalidFormulaString = "=.A1";
        _sheet.Cells.SetFormula(0, 0, invalidFormulaString);
        Assert.False(_sheet.Cells.HasFormula(0, 0));
    }

    [Test]
    public void Set_Cell_Value_Over_Formula_Using_Command_Restores_On_Undo()
    {
        _sheet.Cells.SetValue(1, 1, "Test");
        _sheet.Cells.SetFormula(1, 1, "=10");
        Assert.IsTrue(_sheet.Cells.HasFormula(1, 1));
        Assert.AreEqual(10, _sheet.Cells.GetValue(1, 1));
        _sheet.Commands.ExecuteCommand(new SetCellValueCommand(1, 1, CellValue.Text("TestChange")));
        _sheet.Commands.Undo();
        Assert.AreEqual(10, _sheet.Cells.GetValue(1, 1));
        Assert.IsTrue(_sheet.Cells.HasFormula(1, 1));
        Assert.AreEqual("=10", _sheet.Cells.GetFormulaString(1, 1));
    }

    [Test]
    public void Clear_Cell_Value_Using_Command_Restores_Formula_On_Undo()
    {
        _sheet.Cells.SetFormula(1, 1, "=10");
        _sheet.Commands.ExecuteCommand(new ClearCellsCommand(new Region(1, 1)));
        Assert.False(_sheet.Cells.HasFormula(1, 1));
        _sheet.Commands.Undo();
        Assert.True(_sheet.Cells.HasFormula(1, 1));
        Assert.AreEqual("=10", _sheet.Cells.GetFormulaString(1, 1));
    }

    [Test]
    public void Sum_On_Empty_Cell_Treats_Empty_Cell_As_Zero()
    {
        _sheet.Cells.SetFormula(1, 1, "=A1 + 5");
        var val = _sheet.Cells.GetValue(1, 1);
        val.Should().Be(5);
    }

    [Test]
    public void Set_Cell_Formula_Over_Value_Then_Undo_Restores_Value()
    {
        _sheet.Cells.SetValue(0, 0, 10);
        _sheet.Cells.SetFormula(0, 0, "=5");
        _sheet.Commands.Undo();
        _sheet.Cells.GetValue(0, 0).Should().Be(10);
    }


    [Test]
    public void FormulaEngine_Set_Variable_Calculates()
    {
        _sheet.FormulaEngine.SetVariable("x", 10);
        _sheet.Cells.SetFormula(1, 1, "=x");
        _sheet.Cells.GetValue(1, 1).Should().Be(10);
    }

    [Test]
    public void Formula_Referencing_Range_With_Formula_Recalcs_When_Formula_Recalcs()
    {
        // Cell A1 = 10
        // Cell A2 = 20
        // Cell A3 = Sum(A1:A2)
        // Cell A4 = Sum(A2:A3)
        // When we set A1 and A2, A3 should evaluate first and then A4 because the result of A4 depends on A3
        _sheet.Cells.SetFormula(2, 0, "=AVERAGE(A1:A2)");
        _sheet.Cells.SetFormula(3, 0, "=AVERAGE(A2:A3)");
        _sheet.Cells.SetValue(0, 0, 10); // A1 = 10
        _sheet.Cells.SetValue(1, 0, 20); // A2 = 20
        _sheet.Cells.GetValue(2, 0).Should().Be((10 + 20) / 2d); // A3 = Sum(A1:A2) = 15
        _sheet.Cells.GetValue(3, 0).Should().Be((20 + 15) / 2d); // A4 = Sum(A2:A3) = 17.5
    }

    [Test]
    public void Formula_Referencing_Deleted_Formula_Updates_When_Formula_Is_Cleared_Then_Value_Changes()
    {
        _sheet.Cells.SetFormula(0, 0, "=A2");
        _sheet.Cells.SetFormula(0, 1, "=A1");
        _sheet.Cells.ClearCells(new Region(0, 0));
        _sheet.Cells.SetValue(0, 0, 10);
        _sheet.Cells.GetCellValue(0, 1).GetValue<int>().Should().Be(10);
    }

    [Test]
    public void Formula_Referencing_Deleted_Formula_Updates_When_Formula_Has_Value_Set_Over_It()
    {
        _sheet.Cells.SetFormula(0, 0, "=A2");
        _sheet.Cells.SetFormula(0, 1, "=A1");

        // now override the formula in A1, the formula in B1 (0,1) should update to the new value
        _sheet.Cells.SetValue(0, 0, 10);

        _sheet.Cells.GetCellValue(0, 1).GetValue<int>().Should().Be(10);
    }

    [Test]
    public void Sheet_Should_Not_Recalculate_If_Formula_Removed_From_Sheet()
    {
        _sheet.Cells[0, 0].Formula = "=B1"; // set A1 = B1
        _sheet.Cells[1, 0].Formula = "=A1"; // set A2 = A1
        // override formula at A1 - should remove links from A1 -> B1
        _sheet.Cells.SetValue(0, 0, string.Empty);
        // set value at b1 and ensure sheet doesn't calculate

        int changeCount = 0;

        _sheet.Cells.CellsChanged += (sender, args) => { changeCount++; };
        // change B1
        _sheet.Cells[0, 1].Value = 2;

        changeCount.Should().Be(1);
    }

    [Test]
    public void Insert_Row_Before_Formula_Shifts_Formula_And_Updates_Ref()
    {
        var sheet = new Sheet(10, 10);
        sheet.Cells.SetFormula(2, 2, "=B2"); // set C3 to =B2

        sheet.Rows.InsertAt(1);
        sheet.Cells[2, 2].Formula.Should().BeNull();
        sheet.Cells[3, 2].Formula.Should().Be("=B3");

        sheet.FormulaEngine.DependencyManager.GetDirectDependents(new Region(2, 1), "Sheet1") // b3
            .Select(x => x.Key)
            .First()
            .Should()
            .Be("'Sheet1'!C4"); // (3,2)

        sheet.Commands.Undo();
        sheet.Cells[2, 2].Formula.Should().Be("=B2");

        sheet.FormulaEngine.DependencyManager.GetDirectDependents(new Region(1, 1), "Sheet1") // b2
            .Select(x => x.Key)
            .First()
            .Should()
            .Be("'Sheet1'!C3"); // (3,2)
    }

    [Test]
    public void Insert_Col_Before_Formula_Shifts_Formula_And_Updates_Ref()
    {
        var sheet = new Sheet(10, 10);
        sheet.Cells.SetFormula(2, 2, "=B2");
        sheet.Columns.InsertAt(1);
        sheet.Cells[2, 2].Formula.Should().BeNull();
        sheet.Cells[2, 3].Formula.Should().Be("=C2");
        sheet.Commands.Undo();
        sheet.Cells[2, 2].Formula.Should().Be("=B2");
    }

    [Test]
    public void Remove_Row_Before_Formula_Shifts_Formula_And_Updates_Ref()
    {
        var sheet = new Sheet(10, 10);
        sheet.Cells.SetFormula(2, 2, "=B2");
        sheet.Rows.RemoveAt(0);
        sheet.Cells[2, 2].Formula.Should().BeNull();
        sheet.Cells[1, 2].Formula.Should().Be("=B1");
        sheet.Commands.Undo();
        sheet.Cells[2, 2].Formula.Should().Be("=B2");
    }

    [Test]
    public void Remove_Col_Before_Formula_Shifts_Formula_And_Updates_Ref()
    {
        var sheet = new Sheet(10, 10);
        sheet.Cells.SetFormula(2, 2, "=B2");
        sheet.Columns.RemoveAt(0);
        sheet.Cells[2, 2].Formula.Should().BeNull();
        sheet.Cells[2, 1].Formula.Should().Be("=A2");
        sheet.Commands.Undo();
        sheet.Cells[2, 2].Formula.Should().Be("=B2");
    }

    [Test]
    public void Insert_Row_Before_Formula_Reference_Shifts_Formula_Reference()
    {
        var sheet = new Sheet(10, 10);
        sheet.Cells.SetFormula(2, 2, "=D5");
        sheet.Rows.InsertAt(3, 2);
        sheet.Cells[2, 2].Formula.Should().Be("=D7");
        sheet.Commands.Undo();
        sheet.Cells[2, 2].Formula.Should().Be("=D5");
    }

    [Test]
    public void Insert_Col_Before_Formula_Reference_Shifts_Formula_Reference()
    {
        var sheet = new Sheet(10, 10);
        sheet.Cells.SetFormula(2, 2, "=D5");
        sheet.Columns.InsertAt(3, 2);
        sheet.Cells[2, 2].Formula.Should().Be("=F5");
        sheet.Commands.Undo();
        sheet.Cells[2, 2].Formula.Should().Be("=D5");
    }

    [Test]
    public void Insert_Row_Into_Referenced_Range_Expands_Formula_Reference()
    {
        var sheet = new Sheet(20, 20);
        sheet.Cells.SetFormula(2, 2, "=sum(D5:D10)");
        sheet.Rows.InsertAt(6, 2);
        sheet.Cells[2, 2].Formula.Should().Be("=sum(D5:D12)");
        sheet.Commands.Undo();
        sheet.Cells[2, 2].Formula.Should().Be("=sum(D5:D10)");
    }

    [Test]
    public void Insert_Row_Before_Formula_Allows_Correct_Calculation()
    {
        var sheet = new Sheet(20, 20);
        sheet.Cells.SetFormula(5, 0, "=A1");
        sheet.Rows.InsertAt(2, 1);
        sheet.Range("A1").Value = 2;
        sheet.Cells[6, 0].Value.Should().Be(2);
    }

    [Test]
    public void Insert_Row_Bug_Produces_Multiple_Formula()
    {
        var sheet = new Sheet(20, 20);
        sheet.Cells.SetFormula(5, 0, "=A1");
        sheet.Rows.InsertAt(2, 1);
        sheet.Editor.BeginEdit(6, 0);
        sheet.Editor.EditValue = "=A1";
        sheet.Editor.AcceptEdit();
        sheet.FormulaEngine.DependencyManager.FormulaCount.Should().Be(1);
    }

    [Test]
    public void Remove_Row_Into_Referenced_Range_Contracts_Formula_Reference()
    {
        var sheet = new Sheet(20, 20);
        sheet.Cells.SetFormula(2, 2, "=sum(D5:D10)");
        sheet.Rows.RemoveAt(4, 2);
        sheet.Cells[2, 2].Formula.Should().Be("=sum(D5:D8)");
        sheet.Commands.Undo();
        sheet.Cells[2, 2].Formula.Should().Be("=sum(D5:D10)");
    }

    [Test]
    public void Remove_Formula_And_Undo_Restores_Dependencies()
    {
        var sheet = new Sheet(20, 20);
        sheet.Cells.SetFormula(1, 1, "=5");
        sheet.Cells.SetFormula(0, 0, "=B2");
        sheet.Cells.ClearCells(new Region(0, 0));
        sheet.Commands.Undo();
        sheet.Cells.GetCellValue(0, 0).GetValue<int>().Should().Be(5);
        sheet.FormulaEngine.DependencyManager.HasDependents(1, 1, sheet.Name).Should().BeTrue();
    }

    [Test]
    public void Remove_Formula_In_Column_Removes_And_Restores_Correctly()
    {
        _sheet.Cells.SetFormula(0, 0, "=A2");
        _sheet.Columns.RemoveAt(0);
        _sheet.Commands.Undo();
        _sheet.Cells[0, 0].Formula.Should().Be("=A2");
        _sheet.FormulaEngine.DependencyManager.HasDependents(new Region(1, 0), _sheet.Name).Should().BeTrue();
    }

    [Test]
    public void Conditional_Circular_Reference_Calculates_Correctly()
    {
        // subset of https://github.com/anmcgrath/BlazorDatasheet/issues/126
        var sheet = new Sheet(10, 10);
        sheet.Cells.SetFormula(0, 1, "=if(A1<A3,A1, A1+B2)");
        sheet.Cells.SetFormula(1, 1, "=if(A1<A3,A2+B1,A2+B3)");
        sheet.Cells.SetFormula(2, 1, "=if(A1<A3,A3+B2,A3+B4)");
        sheet.Cells.SetValues(0, 0, [[1], [3], [5]]);
        sheet.Cells[0, 1].Value.Should().Be(1);
        sheet.Cells[1, 1].Value.Should().Be(4);
        sheet.Cells[2, 1].Value.Should().Be(9);
        sheet.BatchUpdates();
        sheet.SortRange(new ColumnRegion(0), [new(0, false)]);
        sheet.EndBatchUpdates();
        sheet.Cells[0, 1].Value.Should().Be(9);
        sheet.Cells[1, 1].Value.Should().Be(4);
        sheet.Cells[2, 1].Value.Should().Be(1);
    }

    [Test]
    public void Simple_Non_Circular_Strongly_Grouped_Reference_Calculates_Correctly()
    {
        var env = new TestEnvironment();
        env.RegisterFunction("if", new IfFunction());
        var eval = new Evaluator(env);
        var parser = new Parser(env);
        var fA1 = parser.FromString("=B1");
        var fB1 = parser.FromString("=if(true,C1,A1)");
        env.SetCellValue(0, 2, 3);
        env.SetCellFormula(0, 0, fA1);
        env.SetCellFormula(0, 1, fB1);
        var res = eval.Evaluate(fB1);
        res.Data.Should().Be(3);
    }

    [Test]
    public void Set_Variable_After_Setting_Formula_Should_Eval_Correctly()
    {
        // We test this because if the parser requires the environmental variable to exist,
        // we will need to re-parse formula when setting a variable.
        _sheet.Cells.SetFormula(1, 1, "=x");
        _sheet.FormulaEngine.SetVariable("x", 10);
        _sheet.Cells.GetValue(1, 1).Should().Be(10);
    }

    //[Test]
    public void Range_Operator_Should_Update_With_Changed_Values()
    {
        _sheet.Cells["A1"]!.Formula = "=sum(a2:b2:c5)";
        _sheet.Cells["C4"]!.Value = 10;
        _sheet.Cells["A1"]!.Value.Should().Be(10);
    }

    [Test]
    public void Variable_With_Formula_Should_Update_When_Variable_Changes()
    {
        _sheet.Cells["A1"]!.Formula = "=x";
        _sheet.FormulaEngine.SetVariable("x", "=10");
        _sheet.Cells["A1"]!.Value.Should().Be(10);
    }

    [Test]
    public void Named_Range_Variable_Should_Update_When_Variable_Changes()
    {
        _sheet.Cells["A1"]!.Formula = "=x";
        _sheet.FormulaEngine.SetVariable("x", "=Sheet1!A2");
        _sheet.Cells["A2"]!.Value = 10;
        _sheet.Cells["A1"]!.Value.Should().Be(10);
    }

    // https://github.com/anmcgrath/BlazorDatasheet/issues/206
    [Test]
    public void Circula_Ref_Error_Depending_On_Order_Of_Edit()
    {
        _sheet.Cells["C1"]!.Formula = "=C9+C10";
        _sheet.Cells["C2"]!.Formula = "=C11-C8";
        _sheet.Cells["C3"]!.Formula = "=C1+C2";
        _sheet.Cells["C4"]!.Formula = "=C8+C3";
        _sheet.Cells["C8"]!.Formula = "=C12/C14*1000";
        _sheet.Cells["C9"]!.Value = 250;
        _sheet.Cells["C10"]!.Value = 130;
        _sheet.Cells["C11"]!.Formula = "=C12+C13";
        _sheet.Cells["C12"]!.Value = 950;
        _sheet.Cells["C13"]!.Value = 456;
        _sheet.Cells["C14"]!.Value = 2;

        _sheet.Cells["C4"]!.Formula = "=C8+C3";
        _sheet.Cells["C4"]!.Value.Should().Be(1786);
    }

    [Test]
    public void Empty_Cell_Should_Equal_Empty_String()
    {
        _sheet.Cells["B1"]!.Formula = "=A1=\"\"";
        _sheet.Cells["B1"]!.Value.Should().Be(true);
    }
}