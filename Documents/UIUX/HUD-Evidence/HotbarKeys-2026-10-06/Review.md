Đã bỏ Keycap khỏi SurvivalHotbar và bỏ thao tác cập nhật panel tương ứng. Phím G/4/5 đặt ở góc trên phải, cách viền 12 đơn vị canvas, có outline chữ. Hình item và số lượng giữ nguyên.

Ảnh và các kiểm tra hotbar đã qua ở 1280×720, 1920×1080, 3440×1440 với UI 100%/125%; không còn panel và chữ không bị cắt. `hotbar-after.png` là ảnh crop từ `sepsis.png` của lượt Play Mode này.

Bài test tổng GameplayStatesAndLayoutRemainReadable chưa qua do `Review mouse reaches the Aim action` tại bước ADS. Job 529fdddf cũng mất kết nối callback do sharing violation ở Library/UnitySkills/batch_state.json; receipt trực tiếp ghi đầy đủ kết quả phía trên. Không thay đổi logic ADS trong yêu cầu này.

Unity biên dịch xong, Console không có lỗi; Editor dừng và cả hai scene sạch. MainMenu giữ nguyên SHA256 6F7123A0845A59AFD92156FBDFE8F02BD052994AB26474FAE04F1718FD258534.
