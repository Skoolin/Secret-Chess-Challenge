use crate::macros::impl_binary_op;

/// Represents a chess board square and bitboard element corresponding to a little-endian rank-file mapping.
///
/// See [LERFM](https://www.chessprogramming.org/Square_Mapping_Considerations#Little-Endian_Rank-File_Mapping) for more information.
#[derive(Default, Debug, Clone, Copy, Eq, PartialEq)]
#[repr(transparent)]
pub struct Square(pub u8);

impl Square {
    pub const NUM: usize = 64;

    /// Returns a `Square` from file and rank coordinates.
    #[inline(always)]
    pub fn from_rank_file(rank: u8, file: u8) -> Self {
        assert!((0..8).contains(&rank));
        assert!((0..8).contains(&file));

        Self(rank * 8 + file)
    }
}

impl_binary_op!(Square, Add, add);
impl_binary_op!(Square, u8, Div, div);
impl_binary_op!(Square, u8, BitXor, bitxor);

impl From<usize> for Square {
    #[inline(always)]
    fn from(value: usize) -> Self {
        assert!((0..64).contains(&value));
        Self(value as u8)
    }
}

impl From<&str> for Square {
    /// Performs the conversion using the algebraic notation.
    ///
    /// The first character is defined to be only `a-h` / `A-H`.
    /// The second character is defined to be only `1-8`.
    fn from(value: &str) -> Self {
        let mut chars = value.chars();

        let file = chars.next().unwrap().to_ascii_lowercase() as u8 - b'a';
        let rank = chars.next().unwrap().to_digit(10).unwrap() as u8 - 1;

        Self::from_rank_file(rank, file)
    }
}

impl<T> std::ops::Index<Square> for [T] {
    type Output = T;

    #[inline(always)]
    fn index(&self, square: Square) -> &Self::Output {
        &self[square.0 as usize]
    }
}

impl<T> std::ops::IndexMut<Square> for [T] {
    #[inline(always)]
    fn index_mut(&mut self, square: Square) -> &mut Self::Output {
        &mut self[square.0 as usize]
    }
}

impl std::fmt::Display for Square {
    fn fmt(&self, f: &mut std::fmt::Formatter) -> std::fmt::Result {
        let rank = self.0 / 8;
        let file = self.0 % 8;

        write!(f, "{}{}", (b'a' + file) as char, rank + 1)
    }
}
