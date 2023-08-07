#[derive(Debug, Clone, Copy, Eq, PartialEq)]
pub enum Color {
    White,
    Black,
}

impl Color {
    pub const NUM: usize = 2;
}

impl Default for Color {
    fn default() -> Self {
        Self::White
    }
}

impl<T> std::ops::Index<Color> for [T] {
    type Output = T;

    #[inline(always)]
    fn index(&self, index: Color) -> &Self::Output {
        &self[index as usize]
    }
}
