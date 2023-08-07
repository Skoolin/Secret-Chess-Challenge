#[rustfmt::skip]
pub enum CastlingKind {
    WhiteShort = 0b0001,
    WhiteLong  = 0b0010,
    BlackShort = 0b0100,
    BlackLong  = 0b1000,
}

#[derive(Default, Debug, Clone, Copy, PartialEq, Eq)]
#[repr(transparent)]
pub struct Castling(pub(crate) u8);

impl Castling {
    /// Allows the specified `CastlingKind`.
    #[inline(always)]
    pub fn allow(&mut self, kind: CastlingKind) {
        self.0 |= kind as u8
    }

    /// Returns `true` if the `CastlingKind` is allowed.
    #[inline(always)]
    pub const fn is_allowed(&self, kind: CastlingKind) -> bool {
        (self.0 & kind as u8) != 0
    }
}

impl From<&str> for Castling {
    fn from(value: &str) -> Self {
        let mut castling = Self::default();

        for c in value.chars() {
            match c {
                '-' => (),
                'K' => castling.allow(CastlingKind::WhiteShort),
                'Q' => castling.allow(CastlingKind::WhiteLong),
                'k' => castling.allow(CastlingKind::BlackShort),
                'q' => castling.allow(CastlingKind::BlackLong),
                _ => panic!("Unexpected castling '{}'", c),
            }
        }

        castling
    }
}
