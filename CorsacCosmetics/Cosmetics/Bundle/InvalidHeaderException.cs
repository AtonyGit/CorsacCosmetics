using System;

namespace CorsacCosmetics.Cosmetics.Bundle;

public class InvalidHeaderException(string message) : Exception(message);