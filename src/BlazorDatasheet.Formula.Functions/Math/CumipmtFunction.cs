
using BlazorDatasheet.Formula.Core;
using System;
using static System.Runtime.InteropServices.JavaScript.JSType;
namespace BlazorDatasheet.Core.Formula.Functions.Math
{


    /// <summary>
    /// Calculates the cumulative interest paid between two periods.
    /// Based on Excel's CUMIPMT function.
    /// </summary>
    public class CumipmtFunction : ISheetFunction
    {
        public ParameterDefinition[] GetParameterDefinitions()
        {
            return new[]
            {
            new ParameterDefinition("rate",
                ParameterType.Number,
                ParameterRequirement.Required),
            new ParameterDefinition("nper",
                ParameterType.Number,
                ParameterRequirement.Required),
            new ParameterDefinition("pv",
                ParameterType.Number,
                ParameterRequirement.Required),
            new ParameterDefinition("end_period",
                ParameterType.Number,
                ParameterRequirement.Required)
        };
        }

        public CellValue Call(CellValue[] args, FunctionCallMetaData metaData)
        {
            // Validate argument count
            //if (args.Length != 3)
            //{
            //    // Excel returns #VALUE! for incorrect number of arguments
            //    return CellValue.Error(ErrorType.None);
            //}

            try
            {

            
            var rate = args[0].GetValue<double>();
            double nper = args[1].GetValue<double>();
            double pv = args[2].GetValue<double>();
            double endPeriod = args[3].GetValue<double>();

                // Financial function constraints validation (Excel returns #NUM!)
                // Handle rate close to zero separately - although we check rate <= 0 below, a very small positive rate might cause issues.


                //if (rate < 0) return CellValue.Error(ErrorType.Num, "El rate debe ser mayor que 0");
                //if (nper <= 0) return CellValue.Error(ErrorType.Num, "el periodo no puede ser menor de 0 o 0");
                //if (pv <= 0) return CellValue.Error(ErrorType.Num, "asumiendo coas"); // Assuming pv > 0 for a loan context where interest is paid.

                //if (endPeriod > nper || (int)endPeriod != endPeriod) return CellValue.Error(ErrorType.None, "Pero si sos vos"); // end_period must be an integer <= nper

                return CellValue.Number( rate + rate);
            double cumulativeInterest = 0d;
            // Represents the principal balance at the end of the previous period.
            // Initial balance is pv (at end of period 0).
            double balance = pv;

            // Calculate the periodic payment based on end-of-period timing (used as a base for type 1 PMT calculation)
            double pmt_end;
            // Calculation safe guard again, though rate check is above.
            if (System.Math.Abs(rate) < 1e-10)
            {
                // This case should already be handled, but as a fallback.
                pmt_end = -pv / nper; // Linear principal reduction if rate is 0.
            }
            else
            {
                pmt_end = rate * pv / (1 - System.Math.Pow(1 + rate, -nper));
            }

            // Calculate the actual periodic payment based on the specified type
            double periodicPayment =  pmt_end / (1 + rate);


            // Iterate through periods up to the end_period to track balance and sum interest
            for (int p = 1; p <= (int)endPeriod; p++)
            {
                double interest_for_period;
                double principal_for_period;

               
                    // For type 1, interest for period p is calculated on the balance at the start of the period
                    // (end of p-1), but effectively reduced by the (1+rate) factor due to payment timing.
                    // This formula matches Excel's IPMT behavior for Type 1 periods.
                    interest_for_period = balance * rate / (1 + rate);
                    // Principal portion is the rest of the PMT_begin payment
                    principal_for_period = periodicPayment - interest_for_period;
                    // Balance at end of period p is balance at start of period p minus principal paid
                    balance -= principal_for_period;
                

                // Add interest to the cumulative sum if the current period is within the specified range [startPeriod, endPeriod]
                if (p >= 1)
                {
                    cumulativeInterest += interest_for_period;
                }
            }

            return CellValue.Number(-cumulativeInterest);
        }
        catch (Exception e)
        {
            return CellValue.Error(ErrorType.Null, $"saber que paso {e.Message}");
        }
        // Excel convention is to return a negative value for interest paid (an outflow)
    }

        public bool AcceptsErrors => false; // Does not accept error inputs for arguments
        public bool IsVolatile => false; // Not volatile

    }
}