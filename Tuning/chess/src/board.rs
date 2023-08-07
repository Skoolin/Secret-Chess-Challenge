use crate::{Bitboard, Castling, Color, Piece, Square};

/// Data structure representing the board and the location of its pieces.
#[derive(Default, Clone)]
pub struct Board {
    pub turn: Color,
    pub pieces: [Bitboard; Piece::NUM],
    pub colors: [Bitboard; Color::NUM],
    pub en_passant: Option<Square>,
    pub castling: Castling,
    /// The phase of the game, ranging from 1.0 (opening) to 0.0 (endgame).
    pub phase: f32,
}

impl Board {
    /// Returns the board corresponding to the specified Forsyth–Edwards notation.
    pub fn new(fen: &str) -> Self {
        parse_fen(fen)
    }

    /// Returns a `Bitboard` for the specified `Piece` type and `Color`.
    #[inline(always)]
    pub fn of(&self, piece: Piece, color: Color) -> Bitboard {
        self.pieces[piece] & self.colors[color]
    }

    /// Returns a `Bitboard` for the specified `Piece` type.
    #[inline(always)]
    pub fn pieces(&self, piece: Piece) -> Bitboard {
        self.pieces[piece]
    }

    /// Finds a piece on the specified `Square` and returns `Some(Piece)`, if found; otherwise `None`.
    #[inline(always)]
    pub fn get_piece(&self, square: Square) -> Option<Piece> {
        for index in 0..Piece::NUM {
            if self.pieces[index].contains(square) {
                return Some(Piece::from(index));
            }
        }
        None
    }

    /// Places a piece of the specified type and color on the square.
    #[inline(always)]
    pub fn add_piece(&mut self, piece: Piece, color: Color, square: Square) {
        self.pieces[piece as usize].set(square);
        self.colors[color as usize].set(square);
    }

    /// Removes a piece of the specified type and color from the square.
    #[inline(always)]
    pub fn remove_piece(&mut self, piece: Piece, color: Color, square: Square) {
        self.pieces[piece as usize].clear(square);
        self.colors[color as usize].clear(square);
    }
}

/// Returns the board corresponding to the specified Forsyth–Edwards notation.
fn parse_fen(fen: &str) -> Board {
    let parts: Vec<&str> = fen.split_whitespace().collect();

    let mut board = Board::default();

    let mut rank = 7;
    let mut file = 0;
    for c in parts[0].chars() {
        if c == '/' {
            rank -= 1;
            file = 0;
        } else if let Some(skip) = c.to_digit(10) {
            file += skip as u8;
        } else {
            let piece = Piece::from(c);
            let color = if c.is_uppercase() {
                Color::White
            } else {
                Color::Black
            };
            let square = Square::from_rank_file(rank, file);

            board.add_piece(piece, color, square);
            file += 1;
        }
    }

    board.turn = match parts[1] {
        "w" => Color::White,
        "b" => Color::Black,
        _ => panic!("Unexpected turn: '{}'", parts[1]),
    };
    board.castling = Castling::from(parts[2]);
    board.en_passant = match parts[3] {
        "-" => None,
        _ => Some(Square::from(parts[3])),
    };

    board.phase = [Piece::Knight, Piece::Bishop, Piece::Rook, Piece::Queen]
        .iter()
        .map(|&piece| board.pieces(piece).count() * [0, 1, 1, 2, 4][piece])
        .sum::<u32>() as f32
        / 24.0;

    board
}
