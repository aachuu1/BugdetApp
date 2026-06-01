namespace BankingApp.Core.Enums;

public enum AccountType { Checking = 0, Savings = 1, Credit = 2 }

public enum TransactionType { Income = 0, Expense = 1, Transfer = 2 }

public enum TransactionCategory
{
    Salary = 0,
    Food = 1,
    Transport = 2,
    Utilities = 3,
    Healthcare = 4,
    Entertainment = 5,
    Shopping = 6,
    Education = 7,
    Housing = 8,
    Insurance = 9,
    Savings = 10,
    Other = 11
}

public enum RecurringFrequency { Daily = 0, Weekly = 1, Monthly = 2, Yearly = 3 }

public enum BillStatus { Pending = 0, Paid = 1, Overdue = 2, Cancelled = 3 }

public enum BillCategory
{
    Electricity = 0,
    Water = 1,
    Gas = 2,
    Internet = 3,
    Phone = 4,
    Rent = 5,
    Insurance = 6,
    Subscription = 7,
    Other = 8
}
