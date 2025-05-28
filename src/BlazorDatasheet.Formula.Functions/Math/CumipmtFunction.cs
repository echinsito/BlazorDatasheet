
using BlazorDatasheet.Formula.Core;
using System;
using static System.Runtime.InteropServices.JavaScript.JSType;
namespace BlazorDatasheet.Core.Formula.Functions.Math
{

    /// <summary>
    /// Calculates the cumulative interest paid on a loan between two specified periods.
    /// </summary>
    /// <param name="InputAnualRate">The interest rate per period (e.g., 0.05/12 for 5% annual rate, monthly payments).</param>
    /// <param name="InputLoanYears">The total number of payment periods in the loan.</param>
    /// <param name="InputLoanAmount">The present value, or the principal amount of the loan.</param>
    /// <param name="InputPaymentYears">The last period in the calculation (must be greater than or equal to start_period and less than or equal to nper).</param>
    /// <returns>The cumulative interest paid, typically a negative value as it represents an outflow.</returns>
    /// CUMIPMT((H2/12/100),I2*12,G2,1,12*AG2,0)
    public class CumipmtFunction : ISheetFunction
    {
        public ParameterDefinition[] GetParameterDefinitions()
        {
            return new[]
            {
            new ParameterDefinition("InputAnualRate",
                ParameterType.Number,
                ParameterRequirement.Required),
            new ParameterDefinition("InputLoanYears",
                ParameterType.Number,
                ParameterRequirement.Required),
            new ParameterDefinition("InputLoanAmount",
                ParameterType.Number,
                ParameterRequirement.Required),
            new ParameterDefinition("InputPaymentYears",
                ParameterType.Number,
                ParameterRequirement.Required)
        };
        }

        public CellValue Call(CellValue[] args, FunctionCallMetaData metaData)
        {

            try
            {

            
            var InputAnualRate = args[0].GetValue<double>();
            int InputLoanYears = args[1].GetValue<int>();
            double InputLoanAmount = args[2].GetValue<double>();
            int InputPaymentYears = args[3].GetValue<int>();

            return CellValue.Number(-FCUMIPMT(InputAnualRate, InputLoanYears, InputLoanAmount, InputPaymentYears));
        }
        catch (Exception e)
        {
            return CellValue.Error(ErrorType.Null, $"saber que paso {e.Message}");
        }
        // Excel convention is to return a negative value for interest paid (an outflow)
    }

        public bool AcceptsErrors => false; // Does not accept error inputs for arguments
        public bool IsVolatile => false; // Not volatile



        public double FCUMIPMT(double InputAnualRate, int InputLoanYears, double InputLoanAmount, int InputPaymentYears)
        {



            double VCumulativeInterest = 0;
            double VBalance = InputLoanAmount;
            double VInitialBalance = InputLoanAmount;
            double VBalancePlusInterest = VInitialBalance;

            string VLog = string.Empty;

            // Calculate the periodic payment. This PMT already accounts for 'type'.
            // PMT will be a negative value as it's an outflow.
            double VLoanPayment = FCalculatePMT(InputAnualRate, InputLoanYears * 12, InputLoanAmount);

            int VTotalPeriods = InputPaymentYears * 12;
            double VPeriodRate = InputAnualRate / 12 / 100;
            // Iterate through each period to simulate the loan amortization
            for (int VPeriod = 1; VPeriod <= VTotalPeriods; VPeriod++)
            {
                double VInterestThisPeriod;
                double VPrincipalPaidThisPeriod;

                VInitialBalance = VBalance;

                // Interest is calculated on the balance at the start of the period.
                VInterestThisPeriod = VBalance * VPeriodRate;

                VBalancePlusInterest = VInitialBalance + VInterestThisPeriod;


                // Principal paid is the total payment minus the interest for this period.
                VPrincipalPaidThisPeriod = VLoanPayment + VInterestThisPeriod;
                // Update the balance by subtracting the principal portion of the payment.
                //VBalance += VPrincipalPaidThisPeriod; // Add principalPaidThisPeriod (which is negative) to balance to reduce it.
                VBalance = VBalancePlusInterest - VLoanPayment;

                // Accumulate interest if the current period falls within the specified range
                if (VPeriod >= 1)
                {
                    VCumulativeInterest += VInterestThisPeriod;
                }
                VLog += $"{VPeriod.ToString().PadLeft(5)}.{VInitialBalance.ToString("C").PadRight(15)}{VInterestThisPeriod.ToString("C").PadRight(15)}{VBalancePlusInterest.ToString("C").PadRight(15)}{VLoanPayment.ToString("C").PadRight(15)}{VBalance.ToString("C").PadRight(15)}{Environment.NewLine}";
            }

            // CUMIPMT typically returns a negative value as it represents an outflow of interest.

            //cumulativeInterest = 100.055M;

            return VCumulativeInterest;
        }

        /// <summary>
        /// Helper function to calculate the periodic payment (PMT) for a loan.
        /// </summary>
        /// <param name="InputAnnualRate">The interest rate per period.</param>
        /// <param name="InputNumberOfPeriods">The total number of payment periods.</param>
        /// <param name="InputLoanAmount">The present value (principal amount of the loan).</param>
        /// <param name="fv">The future value (typically 0 for loans).</param>
        /// <param name="type">The timing of the payment: 0 for end of period, 1 for beginning of period.</param>
        /// <returns>The periodic payment amount (negative as it's an outflow).</returns>
        /// =((A7/100/12)+((A7/100/12)/(POW(1+(A7/100/12),A8*12)-1)))*A6
        /// =((rate/100/12)+((rate/100/12)/(POW(1+(rate/100/12),years*12)-1)))*loan
        private double FCalculatePMT(double InputAnnualRate, int InputNumberOfPeriods, double InputLoanAmount)
        {
            if (InputAnnualRate == 0)
            {
                // If the rate is 0, the payment is simply the principal divided by the number of periods.
                return InputLoanAmount / InputNumberOfPeriods;
            }

            double VPayment;
            // Calculate (1 + rate)^nper, casting to double for Math.Pow, then back to double.
            // (POW(1+(rate/100/12),years*12)-1)
            // double x = (double)Math.Pow((double)(1 + InputRate), InputNumberOfPeriods);

            // // PMT formula derived from financial mathematics, adjusted for Excel's behavior
            // // This formula calculates PMT for end-of-period payments.
            // VPayment = (InputRate * (InputLoanAmount * x )) / (x - 1);

            /// =((InputRate/100/12)+((InputRate/100/12)/(Math.Pow(1+(InputRate/100/12),[years*12||numberOfPeriods])-1)))*InputLoan
            double v = 1 + (InputAnnualRate / 100 / 12);
            /// =((InputRate/100/12)+((InputRate/100/12)/(Math.Pow(1+(InputRate/100/12),[years*12||numberOfPeriods])-1)))*InputLoan
            VPayment = ((InputAnnualRate / 100 / 12) + ((InputAnnualRate / 100 / 12) /
                ((double)System.Math.Pow((double)v, InputNumberOfPeriods) - 1))) * InputLoanAmount;

            string VPaymentStr = VPayment.ToString("0.00");

            VPayment = Convert.ToDouble(VPaymentStr);


            // PMT is typically an outflow, so it's returned as a negative value.
            return VPayment;
        }

        //// Method to trigger calculation from UI
        //private void CalculateCumulativeInterest()
        //{
        //    double VDecimalPaymentAmount = FCalculatePMT(InputAnnualRate, InputLoanYears * 12, InputLoanAmount);
        //    VPaymentAmount = VDecimalPaymentAmount.ToString("C2");

        //    double VDecimalCumulativeInterest = FCUMIPMT(InputAnnualRate, InputLoanYears, InputLoanAmount, InputPaymentYears);
        //    VCumulativeInterest = VDecimalCumulativeInterest.ToString("C");

        //    VTotalPrincipalPaidInPaymentYears = (InputPaymentYears * 12 * VDecimalPaymentAmount) - VDecimalCumulativeInterest;


        //    // ErrorMessage = string.Empty;
        //    // VPaymentAmount = string.Empty;

        //    // try
        //    // {
        //    //     Convert annual rate to periodic rate
        //    //     double periodicRate = 5 / 12 ;
        //    //     int paymentType = IsBeginningOfType ? 1 : 0;

        //    //     double cumulativeInterest = FCUMIPMT(
        //    //         periodicRate,
        //    //         InputNper,
        //    //         InputPv,
        //    //         InputStartPeriod,
        //    //         InputEndPeriod,
        //    //         paymentType
        //    //     );

        //    //     VPaymentAmount = cumulativeInterest.ToString("C2"); Format as currency
        //    // }
        //    // catch (ArgumentException ex)
        //    // {
        //    //     ErrorMessage = ex.Message;
        //    // }
        //    // catch (Exception ex)
        //    // {
        //    //     ErrorMessage = "An unexpected error occurred: " + ex.Message;
        //    // }
        //}

    }
}




    