using System;

namespace CorsacCosmetics.Cosmetics.Bundle;

public class UnsupportedVersionException(string message) : Exception(message);