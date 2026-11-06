using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExampleBcryptDotnet;

// import the Bcrypt.Net library
using BCrypt.Net;

internal class Program
{
    static void Main(string[] args)
    {
        string HashedPassword = BCrypt.HashPassword("mysecretpassword");
        Console.WriteLine(HashedPassword);
        if (BCrypt.Verify("mysecretpassword", HashedPassword))
        {
            Console.WriteLine("Password is correct!");
        }
        else
        {
            Console.WriteLine("Password is incorrect!");
        }
    }
}

